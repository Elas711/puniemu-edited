#!/usr/bin/env python3
"""
Lets the Puni Puni client trust your own certificate authority (for a private server on a real phone
or an emulator without root). Works on your own copy of the game; nothing else is changed.

The game does no certificate pinning of its own; it just uses Android's default, which ignores
user-installed CAs. This tool replaces res/xml/network_security_config.xml in the base APK with the
same file plus:

    <base-config cleartextTrafficPermitted="true">
        <trust-anchors> <certificates src="system"/> <certificates src="user"/> </trust-anchors>
    </base-config>

and signs every split APK with one key (Android refuses splits with different signatures).
Everything else (code, the encrypted libSGF.so, assets) stays byte for byte the same.

  python Tools/patch_apk.py "Yo-kai Watch Puni Puni.apks" --out patched
  adb install-multiple patched/*.apk          (uninstall the store version first: other signature)

Then install your CA as a user certificate on the phone (Settings -> Security -> Encryption &
credentials -> Install a certificate -> CA certificate; the file is certs/ca.pem from
Tools/make_certs.sh) and point the game's host names at your server (see docs/EMULATOR.md).

Needs Java 17+. aapt2 and uber-apk-signer are downloaded from their GitHub releases on first use
(into Tools/.cache) unless --aapt2 / --signer are given.

Not tested on a device here: if the game's protection checks its own signature, a re-signed APK
will not start - tell the Puniemu maintainers.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache")
APKTOOL = "https://github.com/iBotPeaches/Apktool/releases/download/v3.0.3/apktool_3.0.3.jar"
SIGNER = "https://github.com/patrickfav/uber-apk-signer/releases/download/v1.3.0/uber-apk-signer-1.3.0.jar"
NSC = "res/xml/network_security_config.xml"

PATCH = """    <base-config cleartextTrafficPermitted="true">
        <trust-anchors>
            <certificates src="system" />
            <certificates src="user" />
        </trust-anchors>
    </base-config>"""


def fetch(url, name):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        print("downloading", url)
        urllib.request.urlretrieve(url, path + ".part")
        os.replace(path + ".part", path)
    return path


def aapt2_path(arg):
    if arg:
        return arg
    exe = os.path.join(CACHE, "aapt2.exe" if os.name == "nt" else "aapt2")
    if not os.path.exists(exe):
        jar = fetch(APKTOOL, "apktool.jar")
        member = {"nt": "prebuilt/windows/aapt2.exe"}.get(os.name, "prebuilt/macosx/aapt2" if sys.platform == "darwin" else "prebuilt/linux/aapt2")
        with zipfile.ZipFile(jar) as z, open(exe, "wb") as f:
            f.write(z.read(member))
        os.chmod(exe, 0o755)
    return exe


def framework():
    """android.jar-like framework for aapt2 -I: apktool's own framework-res."""
    jar = fetch(APKTOOL, "apktool.jar")
    out = os.path.join(CACHE, "framework.apk")
    if not os.path.exists(out):
        with zipfile.ZipFile(jar) as z:
            name = next(n for n in z.namelist() if n.endswith("android-framework.jar") or n.endswith("/1.apk"))
            with open(out, "wb") as f:
                f.write(z.read(name))
    return out


def decompile_nsc(aapt2, base_apk):
    """The current config as XML text (from aapt2's xmltree dump)."""
    dump = subprocess.run([aapt2, "dump", "xmltree", "--file", NSC, base_apk], capture_output=True, text=True, check=True).stdout
    lines, stack, out = dump.splitlines(), [], ['<?xml version="1.0" encoding="utf-8"?>']
    elems = []  # (depth, tag, attrs, text)
    for l in lines:
        ind = len(l) - len(l.lstrip())
        s = l.strip()
        if s.startswith("E: "):
            elems.append([ind, s[3:].split(" ")[0], [], ""])
        elif s.startswith("A: ") and elems:
            k, v = s[3:].split("=", 1)
            v = v.split(" (Raw")[0].strip('"')
            elems[-1][2].append((k, v))
        elif s.startswith("T: ") and elems:
            elems[-1][3] = s[3:].strip("'")
    # rebuild nesting from indentation
    def render(i, depth):
        ind, tag, attrs, text = elems[i]
        a = "".join(f' {k}="{v}"' for k, v in attrs)
        children, j = [], i + 1
        while j < len(elems) and elems[j][0] > ind:
            if not children or elems[j][0] == elems[children[0]][0]:
                children.append(j)
            j += 1
        pad = "    " * depth
        if not children and not text:
            return [f"{pad}<{tag}{a} />"]
        res = [f"{pad}<{tag}{a}>" + (text if text and not children else "")]
        if children:
            for c in children:
                res += render(c, depth + 1)
            res.append(f"{pad}</{tag}>")
        else:
            res[-1] += f"</{tag}>"
        return res
    out += render(0, 0)
    return "\n".join(out)


def patched_xml(xml):
    import re
    xml = re.sub(r"\s*<base-config[^>]*/>", "", xml)
    xml = re.sub(r"\s*<base-config.*?</base-config>", "", xml, flags=re.S)
    return xml.replace("<network-security-config>", "<network-security-config>\n" + PATCH, 1)


def compile_nsc(aapt2, xml, work):
    res = os.path.join(work, "res", "xml")
    os.makedirs(res, exist_ok=True)
    open(os.path.join(res, "network_security_config.xml"), "w", encoding="utf-8").write(xml)
    manifest = os.path.join(work, "AndroidManifest.xml")
    open(manifest, "w").write('<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="x.nsc">'
                              '<application android:networkSecurityConfig="@xml/network_security_config"/></manifest>')
    subprocess.run([aapt2, "compile", "-o", os.path.join(work, "c.zip"), "--dir", os.path.join(work, "res")], check=True)
    subprocess.run([aapt2, "link", "-o", os.path.join(work, "o.apk"), "-I", framework(), "--manifest", manifest, os.path.join(work, "c.zip")], check=True)
    with zipfile.ZipFile(os.path.join(work, "o.apk")) as z:
        return z.read(NSC)


def replace_entry(src, dst, name, data):
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w") as zout:
        for i in zin.infolist():
            if i.filename.startswith("META-INF/") and i.filename.endswith((".SF", ".RSA", ".EC", ".DSA")) or i.filename == "META-INF/MANIFEST.MF":
                continue  # old v1 signature
            body = data if i.filename == name else zin.read(i)
            zi = zipfile.ZipInfo(i.filename, date_time=i.date_time)
            zi.compress_type, zi.external_attr = i.compress_type, i.external_attr
            zout.writestr(zi, body)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input", help=".apks / .xapk bundle, a base .apk, or a folder with the split APKs")
    ap.add_argument("--out", default="patched")
    ap.add_argument("--aapt2")
    ap.add_argument("--signer", help="uber-apk-signer jar")
    ap.add_argument("--keystore", help="your own keystore (default: uber-apk-signer's debug key)")
    ap.add_argument("--ks-alias")
    ap.add_argument("--ks-pass")
    a = ap.parse_args()
    work = tempfile.mkdtemp(prefix="puniapk_")
    try:
        src = os.path.join(work, "in")
        os.makedirs(src)
        if os.path.isdir(a.input):
            for f in os.listdir(a.input):
                if f.endswith(".apk"):
                    shutil.copy(os.path.join(a.input, f), src)
        else:
            with zipfile.ZipFile(a.input) as z:
                inner = [n for n in z.namelist() if n.endswith(".apk")]
                if inner:  # bundle
                    for n in inner:
                        with open(os.path.join(src, os.path.basename(n)), "wb") as f:
                            f.write(z.read(n))
                else:
                    shutil.copy(a.input, src)
        apks = sorted(os.listdir(src))
        base = next((f for f in apks if NSC in zipfile.ZipFile(os.path.join(src, f)).namelist()), None)
        if not base:
            sys.exit("no APK with a network security config found")
        aapt2 = aapt2_path(a.aapt2)
        xml = patched_xml(decompile_nsc(aapt2, os.path.join(src, base)))
        print(xml)
        data = compile_nsc(aapt2, xml, os.path.join(work, "nsc"))
        unsigned = os.path.join(work, "unsigned")
        os.makedirs(unsigned)
        for f in apks:
            if f == base:
                replace_entry(os.path.join(src, f), os.path.join(unsigned, f), NSC, data)
            else:
                replace_entry(os.path.join(src, f), os.path.join(unsigned, f), None, None)
        signer = a.signer or fetch(SIGNER, "uber-apk-signer.jar")
        cmd = ["java", "-jar", signer, "-a", unsigned, "-o", a.out, "--allowResign"]
        if a.keystore:
            cmd += ["--ks", a.keystore, "--ksAlias", a.ks_alias or "", "--ksPass", a.ks_pass or ""]
        subprocess.run(cmd, check=True)
        print(f"\nsigned APKs in {a.out}: adb install-multiple {a.out}/*.apk")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
