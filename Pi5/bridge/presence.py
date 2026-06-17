"""Node online/offline tracking - the bridge-side port of NodePresenceService.cs.

Watches retained LWT topics `smarthome/status/<node>/online`, updates
esp_nodes.status/last_seen_at, and raises a single DEVICE_OFFLINE alert on the
online->offline transition. A background timer also flips a node offline when its
heartbeat goes stale (mirrors the WPF DispatcherTimer logic).

NOTE: when the WPF app also runs it has its own NodePresenceService doing the same
job, which would double-write. Once this bridge is the always-on hub writer,
disable the DB side of the WPF service (see Pi5/README.md, "Avoid duplicate writes").
"""
from __future__ import annotations

import json
import logging
import threading
from datetime import datetime
from typing import Dict, List, Optional, Tuple

from .db import Database
from .util import utcnow

log = logging.getLogger("bridge.presence")


def parse_online(payload: str) -> bool:
    """Match NodePresenceService.ParseOnline: accept {"online":true}, "online", true, 1."""
    if not payload or not payload.strip():
        return False
    p = payload.strip()
    if '"online"' in p:
        try:
            data = json.loads(p)
            if isinstance(data, dict) and "online" in data:
                return bool(data["online"])
        except (json.JSONDecodeError, TypeError):
            pass
    p = p.strip('"').lower()
    return p in ("online", "true", "1")


class PresenceTracker:
    def __init__(self, db: Database, watched: List[str], presence_topic: str,
                 heartbeat_interval_seconds: int, missed_heartbeats_before_offline: int,
                 auto_create: bool) -> None:
        self._db = db
        self._watched = list(watched)
        self._topic = presence_topic
        self._interval = max(1, heartbeat_interval_seconds)
        self._missed = max(1, missed_heartbeats_before_offline)
        self._auto_create = auto_create
        self._state: Dict[str, Tuple[bool, Optional[datetime]]] = {n: (False, None) for n in self._watched}
        self._lock = threading.Lock()
        self._stop = threading.Event()
        self._thread: Optional[threading.Thread] = None

    def topic_for(self, node: str) -> str:
        return self._topic.replace("{node}", node)

    def subscriptions(self) -> List[str]:
        return [self.topic_for(n) for n in self._watched]

    def node_for_topic(self, topic: str) -> Optional[str]:
        for node in self._watched:
            if topic == self.topic_for(node):
                return node
        return None

    def handle(self, topic: str, payload: str) -> None:
        node = self.node_for_topic(topic)
        if node is None:
            return
        self._apply(node, parse_online(payload), payload)

    def _apply(self, node: str, online: bool, raw: Optional[str]) -> None:
        with self._lock:
            was_online, last = self._state.get(node, (False, None))
            new_last = utcnow() if online else last
            self._state[node] = (online, new_last)
            transition_offline = was_online and not online
            transition = was_online != online
        self._db.update_presence(
            node, online,
            raw if transition_offline else None,
            raise_offline=transition_offline,
            auto_create=self._auto_create,
        )
        if transition:
            log.info("node %s transitioned to %s", node, "online" if online else "offline")

    def start(self) -> None:
        self._thread = threading.Thread(target=self._loop, name="presence-heartbeat", daemon=True)
        self._thread.start()

    def _loop(self) -> None:
        cutoff = self._interval * self._missed
        while not self._stop.wait(self._interval):
            now = utcnow()
            for node, (online, last) in list(self._state.items()):
                if online and last is not None and (now - last).total_seconds() > cutoff:
                    self._apply(node, False, None)

    def stop(self) -> None:
        self._stop.set()
