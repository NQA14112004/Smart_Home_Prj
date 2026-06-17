"""paho-mqtt client wiring: TLS, auth, auto-reconnect, subscriptions, dispatch.

The bridge subscribes to the data topics (handled by handlers.parse) plus the
node presence topics (handled by PresenceTracker). All subscriptions are issued
in on_connect so they are re-applied after every reconnect.
"""
from __future__ import annotations

import logging
import ssl
from typing import Callable

import paho.mqtt.client as mqtt

from .config import Config

log = logging.getLogger("bridge.mqtt")

# Type of the per-message dispatch callback: (topic, payload) -> None
Dispatch = Callable[[str, str], None]


def build_client(cfg: Config, dispatch: Dispatch, subscriptions: list[str]) -> mqtt.Client:
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id=cfg.mqtt.client_id,
        clean_session=True,
    )

    if cfg.mqtt.username:
        client.username_pw_set(cfg.mqtt.username, cfg.mqtt.password)

    if cfg.mqtt.use_tls:
        client.tls_set(ca_certs=cfg.mqtt.ca_cert, tls_version=ssl.PROTOCOL_TLS_CLIENT)

    # Last Will: if the bridge dies, mark it offline (retained) for observers.
    client.will_set(cfg.mqtt.status_topic, '{"online":false}', qos=1, retain=True)

    def on_connect(c, userdata, flags, reason_code, properties=None):
        if reason_code != 0:
            log.error("MQTT connect failed: %s", reason_code)
            return
        log.info("MQTT connected to %s:%s", cfg.mqtt.host, cfg.mqtt.port)
        for topic in subscriptions:
            c.subscribe(topic, qos=1)
            log.info("subscribed: %s", topic)
        c.publish(cfg.mqtt.status_topic, '{"online":true}', qos=1, retain=True)

    def on_disconnect(c, userdata, flags, reason_code, properties=None):
        log.warning("MQTT disconnected: %s (auto-reconnect)", reason_code)

    def on_message(c, userdata, msg):
        try:
            payload = msg.payload.decode("utf-8", "replace")
        except Exception:
            log.warning("could not decode payload on %s", msg.topic)
            return
        try:
            dispatch(msg.topic, payload)
        except Exception:
            log.exception("dispatch failed for topic %s", msg.topic)

    client.on_connect = on_connect
    client.on_disconnect = on_disconnect
    client.on_message = on_message
    client.reconnect_delay_set(min_delay=1, max_delay=30)
    return client
