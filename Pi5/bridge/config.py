"""Configuration loading: secrets from environment/.env, layout from config.yaml.

Precedence mirrors the WPF app's philosophy: real environment variables win, then
.env, then the YAML file (which holds only non-secret layout/tuning values).
Secrets (DB DSN, MQTT username/password) MUST come from env/.env, never YAML.
"""
from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional

import yaml
from dotenv import load_dotenv

from .handlers import HandlerConfig


@dataclass(frozen=True)
class MqttConfig:
    host: str = "127.0.0.1"
    port: int = 8883
    use_tls: bool = True
    ca_cert: Optional[str] = None
    client_id: str = "smarthome-bridge"
    keepalive: int = 30
    username: Optional[str] = None
    password: Optional[str] = None
    # The bridge's own retained presence topic (so other clients see it online).
    status_topic: str = "smarthome/status/bridge/online"


@dataclass(frozen=True)
class NodesConfig:
    door_code: str = "esp32-door"
    home_code: str = "esp32-home"
    face_code: str = "rpi5-face"
    auto_create: bool = True
    presence_topic: str = "smarthome/status/{node}/online"
    heartbeat_interval_seconds: int = 30
    missed_heartbeats_before_offline: int = 3
    watched: List[str] = field(default_factory=lambda: ["esp32-door", "esp32-home", "rpi5-face"])


@dataclass(frozen=True)
class Config:
    database_dsn: str
    mqtt: MqttConfig
    nodes: NodesConfig
    handler: HandlerConfig
    log_level: str = "INFO"


def _require_env(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name} (set it in .env)")
    return value


def load_config(config_path: Optional[str] = None, env_path: Optional[str] = None) -> Config:
    base = Path(__file__).resolve().parent.parent  # the Pi5/ directory
    load_dotenv(env_path or (base / ".env"))

    cfg_file = Path(config_path) if config_path else (base / "config.yaml")
    raw = {}
    if cfg_file.exists():
        raw = yaml.safe_load(cfg_file.read_text(encoding="utf-8")) or {}

    m = raw.get("mqtt", {})
    n = raw.get("nodes", {})
    a = raw.get("alerts", {})

    mqtt = MqttConfig(
        host=m.get("host", "127.0.0.1"),
        port=int(m.get("port", 8883)),
        use_tls=bool(m.get("use_tls", True)),
        ca_cert=m.get("ca_cert"),
        client_id=m.get("client_id", "smarthome-bridge"),
        keepalive=int(m.get("keepalive", 30)),
        username=os.environ.get("MQTT_USERNAME"),
        password=os.environ.get("MQTT_PASSWORD"),
        status_topic=m.get("status_topic", "smarthome/status/bridge/online"),
    )

    nodes = NodesConfig(
        door_code=n.get("door_code", "esp32-door"),
        home_code=n.get("home_code", "esp32-home"),
        face_code=n.get("face_code", "rpi5-face"),
        auto_create=bool(n.get("auto_create", True)),
        presence_topic=n.get("presence_topic", "smarthome/status/{node}/online"),
        heartbeat_interval_seconds=int(n.get("heartbeat_interval_seconds", 30)),
        missed_heartbeats_before_offline=int(n.get("missed_heartbeats_before_offline", 3)),
        watched=list(n.get("watched", ["esp32-door", "esp32-home", "rpi5-face"])),
    )

    handler = HandlerConfig(
        door_code=nodes.door_code,
        home_code=nodes.home_code,
        face_code=nodes.face_code,
        gas_threshold=int(a.get("gas_threshold", 400)),
    )

    return Config(
        database_dsn=_require_env("DATABASE_URL"),
        mqtt=mqtt,
        nodes=nodes,
        handler=handler,
        log_level=str(raw.get("log_level", "INFO")).upper(),
    )
