#!/bin/sh
# Makes a private CA and an HTTPS certificate for the game's server names, so an emulator / test
# phone that trusts the CA can talk to Puniemu without patching the APK (see docs/EMULATOR.md).
#   sh Tools/make_certs.sh certs
set -e
OUT=${1:-certs}
mkdir -p "$OUT"
cd "$OUT"
# strict clients (Android, Python 3.13+) need the CA's key usage written out
[ -f ca.key ] || openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -keyout ca.key -out ca.pem -subj "/CN=Puniemu private server CA" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign" -addext "subjectKeyIdentifier=hash" 2>/dev/null
cat > san.cnf <<CNF
[req]
distinguished_name=dn
[dn]
[ext]
subjectAltName=DNS:gameserver.yw-p.com,DNS:resource.yw-p.com,DNS:api.level5-id.com,DNS:auth.level5-id.com,DNS:pf-api.uniplay.jp,DNS:pf-api-dev.uniplay.jp
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
authorityKeyIdentifier=keyid
CNF
openssl req -newkey rsa:2048 -nodes -keyout server.key -out server.csr -subj "/CN=gameserver.yw-p.com" 2>/dev/null
openssl x509 -req -in server.csr -CA ca.pem -CAkey ca.key -CAcreateserial -days 825 -sha256 -extfile san.cnf -extensions ext -out server.pem 2>/dev/null
openssl pkcs12 -export -inkey server.key -in server.pem -certfile ca.pem -out server.pfx -passout pass:puniemu
# Android wants system CAs named <subject hash>.0
cp ca.pem "$(openssl x509 -inform PEM -subject_hash_old -in ca.pem -noout).0"
echo "certificates in $OUT: server.pfx (password puniemu) for Puniemu, ca.pem / *.0 for the device"
