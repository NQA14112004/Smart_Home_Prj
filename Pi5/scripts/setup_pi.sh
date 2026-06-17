#!/usr/bin/env bash
# One-time setup on a fresh Raspberry Pi OS (64-bit). Run from the Pi5/ directory.
# Installs Mosquitto + PostgreSQL + Python deps and creates the bridge virtualenv.
set -euo pipefail

echo "==> Installing system packages"
sudo apt-get update
sudo apt-get install -y mosquitto mosquitto-clients postgresql python3-venv python3-pip openssl

echo "==> Creating Python virtualenv (.venv) and installing bridge deps"
python3 -m venv .venv
./.venv/bin/pip install --upgrade pip
./.venv/bin/pip install -r requirements.txt

echo "==> Creating Mosquitto users (you will be prompted for passwords)"
echo "    - bridge user (used by the MQTT->DB bridge)"
sudo mosquitto_passwd -c /etc/mosquitto/passwd bridge
echo "    Add more users for each node, e.g.:"
echo "      sudo mosquitto_passwd /etc/mosquitto/passwd esp32-door"
echo "      sudo mosquitto_passwd /etc/mosquitto/passwd esp32-home"
echo "      sudo mosquitto_passwd /etc/mosquitto/passwd wpfclient"

echo
echo "Next steps:"
echo "  1. ./scripts/gen_certs.sh <pi-lan-ip>      # generate self-signed TLS certs"
echo "  2. sudo cp mosquitto/mosquitto.pi.conf /etc/mosquitto/conf.d/smarthome.conf"
echo "  3. sudo systemctl restart mosquitto"
echo "  4. Set up PostgreSQL + restore schema (see scripts/migrate_db.md)"
echo "  5. cp .env.example .env && cp config.example.yaml config.yaml   # then edit both"
echo "  6. ./.venv/bin/python -m bridge.main        # or install the systemd service"
