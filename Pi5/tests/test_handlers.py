"""Unit tests for the pure MQTT->DbWrite translation layer (no DB, no broker)."""
import json

import pytest

from bridge import handlers
from bridge.handlers import HandlerConfig
from bridge.presence import parse_online

CFG = HandlerConfig(door_code="esp32-door", home_code="esp32-home",
                    face_code="rpi5-face", gas_threshold=400)


def test_sensor_home_maps_keys_to_columns():
    w = handlers.parse("smarthome/sensor/home",
                       '{"temperature":30.5,"humidity":72,"gas":380,"light":1250}', CFG)
    assert w is not None
    assert w.table == "sensor_readings"
    assert w.node_code == "esp32-home"
    assert w.values["temperature"] == 30.5
    assert w.values["humidity"] == 72.0
    assert w.values["gas_value"] == 380       # "gas" -> gas_value
    assert w.values["light_value"] == 1250    # "light" -> light_value
    assert json.loads(w.values["raw_payload"])["temperature"] == 30.5


def test_sensor_home_ignores_payload_without_known_keys():
    assert handlers.parse("smarthome/sensor/home", '{"foo":1}', CFG) is None


def test_status_door_sets_door_and_lock():
    w = handlers.parse("smarthome/status/door", '{"door":"closed","lock":"locked"}', CFG)
    assert w.table == "sensor_readings"
    assert w.node_code == "esp32-door"
    assert w.values["door_status"] == "closed"
    assert w.values["lock_status"] == "locked"


def test_rfid_becomes_access_log():
    w = handlers.parse("smarthome/door/rfid", '{"uid":"A1B2C3","result":"success"}', CFG)
    assert w.table == "access_logs"
    assert w.values["method"] == "RFID"
    assert w.values["result"] == "success"
    assert w.values["card_uid"] == "A1B2C3"


def test_keypad_missing_result_defaults_unknown():
    w = handlers.parse("smarthome/door/keypad", '{"attempts":3}', CFG)
    assert w.values["method"] == "PIN"
    assert w.values["result"] == "unknown"
    assert w.values["message"] == "attempts=3"


def test_face_success_requires_user_and_liveness():
    w = handlers.parse("smarthome/face/result",
                       '{"userId":7,"name":"An","confidence":0.92,"live":true}', CFG)
    assert w.node_code == "rpi5-face"
    assert w.values["method"] == "FACE_RECOGNITION"
    assert w.values["result"] == "success"
    assert w.values["user_id"] == 7


def test_face_without_liveness_is_failed():
    w = handlers.parse("smarthome/face/result", '{"userId":7,"live":false}', CFG)
    assert w.values["result"] == "failed"


def test_breach_is_critical_alert():
    w = handlers.parse("smarthome/door/breach", '{"type":"FORCED_ENTRY","accel":2.1}', CFG)
    assert w.table == "alerts"
    assert w.values["alert_type"] == "FORCED_ENTRY"
    assert w.values["level"] == "critical"
    assert w.values["value"] == "2.1"
    assert w.values["is_resolved"] is False


def test_gas_level_depends_on_threshold():
    high = handlers.parse("smarthome/alarm/gas", '{"type":"GAS_HIGH","value":500}', CFG)
    low = handlers.parse("smarthome/alarm/gas", '{"type":"GAS_HIGH","value":150}', CFG)
    assert high.values["level"] == "critical"
    assert low.values["level"] == "warning"
    assert high.values["threshold"] == "400"


def test_unknown_topic_returns_none():
    assert handlers.parse("smarthome/unknown/thing", "{}", CFG) is None


def test_malformed_json_does_not_crash():
    assert handlers.parse("smarthome/sensor/home", "not-json", CFG) is None
    w = handlers.parse("smarthome/door/rfid", "not-json", CFG)
    assert w.values["result"] == "unknown"   # access log still recorded


@pytest.mark.parametrize("payload,expected", [
    ('{"online":true}', True),
    ('{"online":false}', False),
    ("online", True),
    ("true", True),
    ("1", True),
    ("offline", False),
    ("", False),
])
def test_parse_online_variants(payload, expected):
    assert parse_online(payload) is expected
