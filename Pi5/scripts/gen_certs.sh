#!/usr/bin/env bash
# Generate a self-signed CA + server certificate for Mosquitto TLS (port 8883).
# Usage: ./scripts/gen_certs.sh <PI_LAN_IP_OR_HOSTNAME>
# Output goes to ./certs/ ; copy to /etc/mosquitto/certs/ as shown at the end.
set -euo pipefail

CN="${1:?Usage: gen_certs.sh <PI_LAN_IP_OR_HOSTNAME>}"
OUT="certs"
DAYS=3650
mkdir -p "$OUT"

echo "==> CA key + cert"
openssl genrsa -out "$OUT/ca.key" 2048
openssl req -new -x509 -days "$DAYS" -key "$OUT/ca.key" -out "$OUT/ca.crt" \
  -subj "/C=VN/O=SmartHome/CN=SmartHome-CA"

echo "==> Server key + CSR (CN=$CN)"
openssl genrsa -out "$OUT/server.key" 2048
openssl req -new -key "$OUT/server.key" -out "$OUT/server.csr" \
  -subj "/C=VN/O=SmartHome/CN=$CN"

echo "==> Sign server cert with SAN=$CN"
cat > "$OUT/ext.cnf" <<EXT
subjectAltName = $(echo "$CN" | grep -Eq '^[0-9.]+$' && echo "IP:$CN" || echo "DNS:$CN")
EXT
openssl x509 -req -in "$OUT/server.csr" -CA "$OUT/ca.crt" -CAkey "$OUT/ca.key" \
  -CAcreateserial -out "$OUT/server.crt" -days "$DAYS" -extfile "$OUT/ext.cnf"

echo
echo "Install on the Pi:"
echo "  sudo mkdir -p /etc/mosquitto/certs"
echo "  sudo cp $OUT/ca.crt $OUT/server.crt $OUT/server.key /etc/mosquitto/certs/"
echo "  sudo chown mosquitto: /etc/mosquitto/certs/*"
echo "Copy ca.crt to each MQTT client (WPF MqttOptions.CaCertPath, ESP32 firmware, bridge config.yaml)."
