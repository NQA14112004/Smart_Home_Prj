"""Entry point: wire config -> DB -> presence -> MQTT and run forever.

Run from the Pi5/ directory:   python -m bridge.main
Or via systemd (see systemd/smarthome-bridge.service).
"""
from __future__ import annotations

import logging
import signal
import sys

from . import handlers
from .config import Config, load_config
from .db import Database
from .mqtt_client import build_client
from .presence import PresenceTracker


def setup_logging(level: str) -> None:
    logging.basicConfig(
        level=getattr(logging, level, logging.INFO),
        format="%(asctime)s %(levelname)-7s %(name)s: %(message)s",
    )


def build_dispatch(cfg: Config, db: Database, presence: PresenceTracker):
    log = logging.getLogger("bridge")

    def dispatch(topic: str, payload: str) -> None:
        # Presence (LWT) topics are stateful and handled separately from data.
        if presence.node_for_topic(topic) is not None:
            presence.handle(topic, payload)
            return
        write = handlers.parse(topic, payload, cfg.handler)
        if write is None:
            log.debug("ignored topic %s", topic)
            return
        db.apply(write, cfg.nodes.auto_create)

    return dispatch


def main() -> int:
    cfg = load_config()
    setup_logging(cfg.log_level)
    log = logging.getLogger("bridge")
    log.info("Smart Home MQTT->DB bridge starting")

    db = Database(cfg.database_dsn)
    try:
        db.connect()
    except Exception:
        log.exception("initial DB connection failed; will retry lazily on first write")

    presence = PresenceTracker(
        db,
        watched=cfg.nodes.watched,
        presence_topic=cfg.nodes.presence_topic,
        heartbeat_interval_seconds=cfg.nodes.heartbeat_interval_seconds,
        missed_heartbeats_before_offline=cfg.nodes.missed_heartbeats_before_offline,
        auto_create=cfg.nodes.auto_create,
    )

    subscriptions = list(handlers.DATA_TOPICS) + presence.subscriptions()
    dispatch = build_dispatch(cfg, db, presence)
    client = build_client(cfg, dispatch, subscriptions)

    def shutdown(signum, frame):
        log.info("signal %s received, shutting down", signum)
        presence.stop()
        try:
            client.disconnect()
        except Exception:
            pass

    signal.signal(signal.SIGINT, shutdown)
    signal.signal(signal.SIGTERM, shutdown)

    presence.start()
    client.connect(cfg.mqtt.host, cfg.mqtt.port, keepalive=cfg.mqtt.keepalive)
    client.loop_forever()

    db.close()
    log.info("bridge stopped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
