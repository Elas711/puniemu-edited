# Testing the real game on a PC (Android emulator, no APK patching)

The 4.177.0 client's code is encrypted and its server name is not stored as plain text, so the APK
cannot simply be pointed at another server. An emulator can be redirected instead: its hosts file
sends `gameserver.yw-p.com` (and the other game hosts) to your PC, and Puniemu answers them over
HTTPS with a certificate from your own CA, installed into the emulator's system store.

Tested here: the certificate set-up and Puniemu answering game requests over HTTPS as
`gameserver.yw-p.com` (a client that trusts the CA gets normal responses). Not tested: the game
itself on an emulator (this needs a PC with hardware virtualisation). If the app pins its
certificates or checks its integrity, this will not be enough.

## 1. Emulator

* Android Studio -> Device Manager -> new device, system image **Android 11, 12 or 13, x86_64,
  "Google APIs"** (not "Google Play": those cannot be rooted). These images run ARM-only apps such
  as Puni Puni through Google's ARM translation. (Android 14+ keeps the CA store in an APEX module,
  which makes step 3 harder.)
* Start it writable: `emulator -avd <name> -writable-system`
* `adb root && adb remount` (the first time also `adb disable-verity`, `adb reboot`, `adb root`, `adb remount`)

## 2. Install the game

The download is a split bundle (`.apks`). Unzip it and install the parts together:

```
adb install-multiple com.Level5.YWP.apk config.arm64_v8a.apk config.en.apk config.hdpi.apk
```

## 3. Certificates

```
sh Tools/make_certs.sh certs          # CA + certificate for the game's host names
adb push certs/*.0 /system/etc/security/cacerts/
adb shell chmod 644 /system/etc/security/cacerts/*.0
```

## 4. Redirect the game's hosts to your PC

`10.0.2.2` is the PC as seen from the emulator:

```
adb shell "echo '10.0.2.2 gameserver.yw-p.com resource.yw-p.com api.level5-id.com auth.level5-id.com pf-api.uniplay.jp pf-api-dev.uniplay.jp' >> /system/etc/hosts"
adb reboot
```

## 5. Run Puniemu on HTTPS port 443

```
ASPNETCORE_URLS="https://0.0.0.0:443;http://0.0.0.0:5000" \
ASPNETCORE_Kestrel__Certificates__Default__Path=certs/server.pfx \
ASPNETCORE_Kestrel__Certificates__Default__Password=puniemu \
dotnet Puniemu.dll
```

(port 443 needs administrator rights; on Windows set the variables with `set` in an admin prompt.)
Set `"PublicServerURL": "https://gameserver.yw-p.com"` so the links the server hands out use the
redirected name, and serve the data download from the same server (`ServeDataDownload`).

Then start the game. Requests Puniemu does not implement yet are listed in the admin panel
(Logs -> Unimplemented requests) with their decrypted body - that is how the missing calls of
docs/CLIENT_ENDPOINTS.md can be filled in.

## Real phone (no root): patched APK

`Tools/patch_apk.py` changes one file of your own APK, the network security config, so the game
also trusts user-installed CAs (and allows plain HTTP), then signs all split APKs with one key:

```
python Tools/patch_apk.py "Yo-kai Watch Puni Puni.apks" --out patched
adb uninstall com.Level5.YWP          # the store version has another signature (deletes local data!)
adb install-multiple patched/*.apk
```

On the phone: install `certs/ca.pem` as a CA certificate (Settings -> Security -> Encryption &
credentials -> Install a certificate -> CA certificate) and point the game's host names at your
server with a DNS you control (Pi-hole, your router, or a local DNS app).

Verified here: the four split APKs are signed with the same key (v1/v2/v3), zip-aligned, the 16
native libraries stay uncompressed and page-aligned, and only `res/xml/network_security_config.xml`
differs from the original. Not verified: whether the game's protection accepts a re-signed APK.

## Wibble Wobble clients

Wibble Wobble reads its server from the NHN HSP launching config (`res/xml/hsp_launching_zone.xml`
on Android, `HSPLaunchingZone.plist` on iOS): the active zone's address + `/hsp` answers
`getLaunchingInfos`, which names the game server. Puniemu answers `/hsp/...getLaunchingInfos`
itself, so point the active zone at `http://<your server>:<port>/hsp`. The web tool
[WibWobPS/wwpatcher](https://github.com/WibWobPS/wwpatcher) does exactly that for `.apk` and `.ipa`
(and re-signs). Puni Puni 4.177.0 has no launching zone file; see the sections above.
