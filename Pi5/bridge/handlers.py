"""Pure MQTT-payload -> DB-write translation.

No I/O lives here. Every handler takes the raw (topic, payload) plus a small
HandlerConfig and returns a `DbWrite` describing *what* to persist (which table,
which node, which column values). `db.py` resolves the node_code to a node_id and
runs the INSERT. Because these functions are side-effect free they are unit-tested
in tests/test_handlers.py without a broker or a database.

Topics mirror Smart_Home/Services/MqttTopics.cs; payload shapes mirror Phụ lục A
of PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, Callable, Dict, Optional

from .util import as_bool, as_float, as_int, as_str

# --- Topic constants (keep in sync with MqttTopics.cs) ----------------------
SENSOR_HOME = "smarthome/sensor/home"
STATUS_DOOR = "smarthome/status/door"
DOOR_RFID = "smarthome/door/rfid"
DOOR_KEYPAD = "smarthome/door/keypad"
DOOR_BREACH = "smarthome/door/breach"
ALARM_GAS = "smarthome/alarm/gas"
FACE_RESULT = "smarthome/face/result"

# Tables this bridge is allowed to write (whitelist; never user-derived).
SENSOR_READINGS = "sensor_readings"
ACCESS_LOGS = "access_logs"
ALERTS = "alerts"


@dataclass(frozen=True)
class HandlerConfig:
    """The few knobs handlers need. Built from config.yaml in config.py."""
    door_code: str = "esp32-door"
    home_code: str = "esp32-home"
    face_code: str = "rpi5-face"
    gas_threshold: int = 400  # gas_value >= this -> critical, otherwise warning


@dataclass(frozen=True)
class DbWrite:
    """A normalized, table-agnostic INSERT request.

    `node_code` is resolved to a real esp_nodes.id by the DB layer; `values`
    holds column->value pairs (a `raw_payload` key is cast to jsonb on insert).
    """
    table: str
    node_code: Optional[str]
    values: Dict[str, Any]


def _loads(payload: str) -> Dict[str, Any]:
    try:
        data = json.loads(payload)
    except (json.JSONDecodeError, TypeError):
        return {}
    return data if isinstance(data, dict) else {}


def _handle_sensor_home(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    known = ("temperature", "humidity", "gas", "light", "motion")
    if not any(k in d for k in known):
        return None
    return DbWrite(SENSOR_READINGS, cfg.home_code, {
        "temperature": as_float(d.get("temperature")),
        "humidity": as_float(d.get("humidity")),
        "gas_value": as_int(d.get("gas")),
        "light_value": as_int(d.get("light")),
        "motion_detected": as_bool(d.get("motion")),
        "raw_payload": payload,
    })


def _handle_status_door(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    if "door" not in d and "lock" not in d:
        return None
    return DbWrite(SENSOR_READINGS, cfg.door_code, {
        "door_status": as_str(d.get("door")),
        "lock_status": as_str(d.get("lock")),
        "raw_payload": payload,
    })


def _handle_rfid(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    return DbWrite(ACCESS_LOGS, cfg.door_code, {
        "method": "RFID",
        "result": as_str(d.get("result")) or "unknown",
        "card_uid": as_str(d.get("uid")),
        "raw_payload": payload,
    })


def _handle_keypad(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    message = f"attempts={d.get('attempts')}" if "attempts" in d else None
    return DbWrite(ACCESS_LOGS, cfg.door_code, {
        "method": "PIN",
        "result": as_str(d.get("result")) or "unknown",
        "message": message,
        "raw_payload": payload,
    })


def _handle_face(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    user_id = as_int(d.get("userId"))
    live = bool(as_bool(d.get("live")))
    result = "success" if (user_id is not None and live) else "failed"
    parts = []
    if d.get("name"):
        parts.append(str(d["name"]))
    if d.get("confidence") is not None:
        parts.append(f"conf={d['confidence']}")
    if not live:
        parts.append("liveness=failed")
    return DbWrite(ACCESS_LOGS, cfg.face_code, {
        "method": "FACE_RECOGNITION",
        "result": result,
        "user_id": user_id,
        "message": ", ".join(parts) or None,
        "raw_payload": payload,
    })


def _handle_breach(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    return DbWrite(ALERTS, cfg.door_code, {
        "alert_type": as_str(d.get("type")) or "FORCED_ENTRY",
        "level": "critical",
        "message": "Phát hiện cạy/phá cửa!",
        "value": as_str(d.get("accel")),
        "raw_payload": payload,
        "is_resolved": False,
    })


def _handle_gas(payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    d = _loads(payload)
    value = as_int(d.get("value"))
    level = "critical" if (value is not None and value >= cfg.gas_threshold) else "warning"
    return DbWrite(ALERTS, cfg.home_code, {
        "alert_type": as_str(d.get("type")) or "GAS_HIGH",
        "level": level,
        "message": "Phát hiện nồng độ gas cao!",
        "value": as_str(value),
        "threshold": str(cfg.gas_threshold),
        "raw_payload": payload,
        "is_resolved": False,
    })


_HANDLERS: Dict[str, Callable[[str, HandlerConfig], Optional[DbWrite]]] = {
    SENSOR_HOME: _handle_sensor_home,
    STATUS_DOOR: _handle_status_door,
    DOOR_RFID: _handle_rfid,
    DOOR_KEYPAD: _handle_keypad,
    DOOR_BREACH: _handle_breach,
    ALARM_GAS: _handle_gas,
    FACE_RESULT: _handle_face,
}

# Data topics this bridge subscribes to (presence topics are added separately).
DATA_TOPICS = tuple(_HANDLERS.keys())


def parse(topic: str, payload: str, cfg: HandlerConfig) -> Optional[DbWrite]:
    """Translate one MQTT message to a DbWrite, or None if the topic is ignored
    or the payload carries nothing persistable."""
    handler = _HANDLERS.get(topic)
    if handler is None:
        return None
    return handler(payload, cfg)
