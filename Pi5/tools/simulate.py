"""Publish sample MQTT messages so you can verify the bridge writes to the DB
without any ESP32 or camera hardware.

Usage (from Pi5/, with .env filled in):
    python -m tools.simulate                # one of each message + nodes online
    python -m tools.simulate --offline      # also flip nodes offline (DEVICE_OFFLINE)

Then check the DB or open the WPF dashboard:
    SELECT * FROM sensor_readings ORDER BY created_at DESC LIMIT 5;
    SELECT * FROM access_logs   ORDER BY created_at DESC LIMIT 5;
    SELECT * FROM alerts        ORDER BY created_at DESC LIMIT 5;
"""
from __future__ import annotations

import argparse
import os
import ssl
import sys
import time
from pathlib import Path

import paho.mqtt.client as mqtt
import yaml
from dotenv import load_dotenv

BASE = Path(__file__).resolve().parent.parent

SAMPLES = [
    ("smarthome/status/esp32-door/online", '{"online":true}', True),
    ("smarthome/status/esp32-home/online", '{"online":true}', True),
    ("smarthome/sensor/home", '{"temperature":30.5,"humidity":72,"gas":380,"light":1250}', False),
    ("smarthome/status/door", '{"door":"closed","lock":"locked"}', False),
    ("smarthome/door/rfid", '{"uid":"A1B2C3","result":"success"}', False),
    ("smarthome/door/keypad", '{"result":"failed","attempts":3}', False),
    ("smarthome/face/result", '{"userId":1,"name":"Test User","confidence":0.93,"live":true}', False),
    ("smarthome/alarm/gas", '{"type":"GAS_HIGH","value":510}', False),
    ("smarthome/door/breach", '{"type":"FORCED_ENTRY","accel":2.4}', False),
]

OFFLINE = [
    ("smarthome/status/esp32-door/online", '{"online":false}', True),
    ("smarthome/status/esp32-home/online", '{"online":false}', True),
]


def load_settings():
    load_dotenv(BASE / ".env")
    cfg_file = BASE / "config.yaml"
    raw = yaml.safe_load(cfg_file.read_text(encoding="utf-8")) if cfg_file.exists() else {}
    return raw.get("mqtt", {})


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--offline", action="store_true", help="also publish node-offline messages")
    args = ap.parse_args()

    m = load_settings()
    client = mqtt.Client(callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
                         client_id="smarthome-simulator")
    user = os.environ.get("MQTT_USERNAME")
    if user:
        client.username_pw_set(user, os.environ.get("MQTT_PASSWORD"))
    if bool(m.get("use_tls", True)):
        client.tls_set(ca_certs=m.get("ca_cert"), tls_version=ssl.PROTOCOL_TLS_CLIENT)

    client.connect(m.get("host", "127.0.0.1"), int(m.get("port", 8883)), keepalive=30)
    client.loop_start()

    messages = list(SAMPLES) + (OFFLINE if args.offline else [])
    for topic, payload, retain in messages:
        client.publish(topic, payload, qos=1, retain=retain)
        print(f"-> {topic}  {payload}")
        time.sleep(0.2)

    time.sleep(1.0)
    client.loop_stop()
    client.disconnect()
    print(f"published {len(messages)} messages")
    return 0


if __name__ == "__main__":
    sys.exit(main())
