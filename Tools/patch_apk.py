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
  ... --game-url https://ab.de/ --account-url http://192.168.1.50:8080   (server address in the game, see docs/EMULATOR.md)
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


# ---- server address inside libSGF.so ----------------------------------------------------------
# The game's code in libSGF.so is encrypted, but its strings are not, so they can be replaced in
# place: same position, a new text that is not longer, the rest filled with zero bytes.
#   https://%s.com/   the game server ("%s" is filled in at runtime, e.g. gameserver.yw-p);
#                     15 characters at most, e.g. https://ab.de/ or http://ab.de/ (port 80 / 443)
#   https://pf-api-dev.uniplay.jp/api/v1/{active,create_gdkey}?apkey=   the account server; room for
#                     a base of up to 29 characters incl. a port, e.g. http://192.168.1.50:8080
GAME_FMT = b"https://%s.com/"
ACCOUNT_HOST = b"https://pf-api-dev.uniplay.jp"
ACCOUNT_PATHS = [b"/api/v1/active?apkey=", b"/api/v1/create_gdkey?apkey="]


def replace_cstring(data, old, new, what):
    """Replaces the zero-terminated string `old` (must occur exactly once) by `new`, zero-padded."""
    if len(new) > len(old):
        sys.exit(f"{what}: '{new.decode()}' is {len(new)} characters, at most {len(old)} fit in the game")
    hits, i = [], data.find(old + b"\0")
    while i >= 0:
        if i == 0 or data[i - 1] == 0:  # a whole string, not the end of a longer one
            hits.append(i)
        i = data.find(old + b"\0", i + 1)
    if len(hits) != 1:
        sys.exit(f"{what}: expected the string {old!r} once in libSGF.so, found it {len(hits)} times (other game version?)")
    i = hits[0]
    return data[:i] + new + b"\0" * (len(old) - len(new)) + data[i + len(old):], i


def patch_server_strings(lib, game_url, account_url):
    if game_url:
        g = game_url.encode("ascii")
        if not g.startswith((b"http://", b"https://")) or not g.endswith(b"/"):
            sys.exit("--game-url must look like http://host/ or https://host/ (with the final /)")
        lib, at = replace_cstring(lib, GAME_FMT, g, "--game-url")
        print(f"libSGF.so @0x{at:x}: {GAME_FMT.decode()} -> {game_url}")
    if account_url:
        base = account_url.rstrip("/").encode("ascii")
        for path in ACCOUNT_PATHS:
            lib, at = replace_cstring(lib, ACCOUNT_HOST + path, base + path, "--account-url")
            print(f"libSGF.so @0x{at:x}: {(ACCOUNT_HOST + path).decode()} -> {(base + path).decode()}")
    return lib


def replace_entry(src, dst, name, data):
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w") as zout:
        for i in zin.infolist():
            if i.filename.startswith("META-INF/") and i.filename.endswith((".SF", ".RSA", ".EC", ".DSA")) or i.filename == "META-INF/MANIFEST.MF":
                continue  # old v1 signature
            body = data if i.filename == name else zin.read(i)
            zi = zipfile.ZipInfo(i.filename, date_time=i.date_time)
            zi.compress_type, zi.external_attr = i.compress_type, i.external_attr
            zout.writestr(zi, body)


def replace_entries(src, dst, changes):
    """Copies an APK, replacing the entries in `changes` (same compression: native libraries stay stored)."""
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w") as zout:
        for i in zin.infolist():
            if i.filename.startswith("META-INF/") and i.filename.endswith((".SF", ".RSA", ".EC", ".DSA")) or i.filename == "META-INF/MANIFEST.MF":
                continue  # old v1 signature
            body = changes.get(i.filename) if i.filename in changes else zin.read(i)
            zi = zipfile.ZipInfo(i.filename, date_time=i.date_time)
            zi.compress_type, zi.external_attr = i.compress_type, i.external_attr
            zout.writestr(zi, body)


def make_xapk(apk_dir, xapk_path, aapt2):
    """Packs the signed split APKs into one .xapk (zip + manifest.json, as APKPure / XAPK installers read it)."""
    import json
    import re
    apks = sorted(f for f in os.listdir(apk_dir) if f.endswith(".apk"))
    info = {}
    for f in apks:
        out = subprocess.run([aapt2, "dump", "badging", os.path.join(apk_dir, f)], capture_output=True, text=True).stdout
        split = re.search(r"split='([^']+)'", out.splitlines()[0] if out else "")
        info[f] = (split.group(1) if split else "base", out)
    base = next(f for f, (sid, _) in info.items() if sid == "base")
    badging = info[base][1]
    def field(pattern, default=""):
        m = re.search(pattern, badging)
        return m.group(1) if m else default
    def clean_name(f):
        sid = info[f][0]
        return (field(r"package: name='([^']+)'") if sid == "base" else sid) + ".apk"
    manifest = {
        "xapk_version": 2,
        "package_name": field(r"package: name='([^']+)'"),
        "name": field(r"application-label:'([^']*)'", "app"),
        "version_code": field(r"versionCode='([^']+)'"),
        "version_name": field(r"versionName='([^']+)'"),
        "min_sdk_version": field(r"minSdkVersion:'(\d+)'", "21"),
        "target_sdk_version": field(r"targetSdkVersion:'(\d+)'", "34"),
        "permissions": re.findall(r"uses-permission: name='([^']+)'", badging),
        "split_configs": [info[f][0] for f in apks if info[f][0] != "base"],
        "total_size": sum(os.path.getsize(os.path.join(apk_dir, f)) for f in apks),
        "split_apks": [{"file": clean_name(f), "id": info[f][0]} for f in [base] + [f for f in apks if f != base]],
    }
    icon = None
    with zipfile.ZipFile(os.path.join(apk_dir, base)) as z:
        cands = sorted((i for i in z.infolist() if re.search(r"mipmap-x+hdpi[^/]*/ic_launcher\.(webp|png)$", i.filename)), key=lambda i: -i.file_size)
        if cands:
            raw = z.read(cands[0])
            try:
                import io
                from PIL import Image
                buf = io.BytesIO()
                Image.open(io.BytesIO(raw)).convert("RGBA").save(buf, "PNG")
                icon = buf.getvalue()
            except Exception:  # noqa: BLE001 - no Pillow: an .xapk without icon still installs
                icon = raw if cands[0].filename.endswith(".png") else None
    if icon:
        manifest["icon"] = "icon.png"
    with zipfile.ZipFile(xapk_path, "w") as zx:
        zx.writestr("manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2), compress_type=zipfile.ZIP_DEFLATED)
        if icon:
            zx.writestr("icon.png", icon)
        for entry in manifest["split_apks"]:
            src = base if entry["id"] == "base" else next(f for f in apks if info[f][0] == entry["id"])
            zx.write(os.path.join(apk_dir, src), entry["file"], compress_type=zipfile.ZIP_STORED)
    print(f"{xapk_path}: {len(apks)} APKs, {manifest['package_name']} {manifest['version_name']}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input", help=".apks / .xapk bundle, a base .apk, or a folder with the split APKs")
    ap.add_argument("--out", default="patched")
    ap.add_argument("--aapt2")
    ap.add_argument("--signer", help="uber-apk-signer jar")
    ap.add_argument("--keystore", help="your own keystore (default: uber-apk-signer's debug key)")
    ap.add_argument("--ks-alias")
    ap.add_argument("--ks-pass")
    ap.add_argument("--game-url", help="replace the game server address in libSGF.so (at most 15 characters, e.g. https://ab.de/)")
    ap.add_argument("--xapk", help="also pack the result into one .xapk file (for XAPK installers / BlueStacks)")
    ap.add_argument("--account-url", help="replace the account server address in libSGF.so (at most 29 characters, e.g. http://192.168.1.50:8080)")
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
        lib_apk = lib_name = lib_data = None
        if a.game_url or a.account_url:
            for f in apks:
                with zipfile.ZipFile(os.path.join(src, f)) as z:
                    names = [n for n in z.namelist() if n.endswith("/libSGF.so")]
                    if names:
                        lib_apk, lib_name = f, names[0]
                        lib_data = patch_server_strings(z.read(names[0]), a.game_url, a.account_url)
                        break
            if lib_data is None:
                sys.exit("libSGF.so not found in the APKs")
        for f in apks:
            changes = {}
            if f == base:
                changes[NSC] = data
            if f == lib_apk:
                changes[lib_name] = lib_data
            replace_entries(os.path.join(src, f), os.path.join(unsigned, f), changes)
        signer = a.signer or fetch(SIGNER, "uber-apk-signer.jar")
        cmd = ["java", "-jar", signer, "-a", unsigned, "-o", a.out, "--allowResign"]
        if a.keystore:
            cmd += ["--ks", a.keystore, "--ksAlias", a.ks_alias or "", "--ksPass", a.ks_pass or ""]
        subprocess.run(cmd, check=True)
        for f in os.listdir(a.out):
            if f.endswith(".idsig"):
                os.remove(os.path.join(a.out, f))  # v4 signature side files, not needed to install
        print(f"\nsigned APKs in {a.out}: adb install-multiple {a.out}/*.apk")
        if a.xapk:
            make_xapk(a.out, a.xapk, aapt2)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
