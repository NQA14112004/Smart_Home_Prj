"""PostgreSQL access for the bridge.

A single autocommit psycopg3 connection guarded by a re-entrant lock (paho fires
callbacks on its network thread, and the heartbeat timer runs on its own thread).
The connection is lazily reopened so a transient DB outage does not kill the
bridge. Table names come from a fixed whitelist in handlers.py and column keys
come from our own handlers, so the dynamic INSERT is not exposed to user input.
"""
from __future__ import annotations

import logging
import threading
from typing import Any, Dict, List, Optional

import psycopg

from .handlers import ACCESS_LOGS, DbWrite
from .util import utcnow

log = logging.getLogger("bridge.db")

# Human-readable node metadata used only when auto-creating a missing row.
_NODE_DEFAULTS = {
    "esp32-door": ("Cua chinh", "esp32"),
    "esp32-home": ("Nha", "esp32"),
    "rpi5-face": ("Raspberry Pi 5 - nhan dien", "raspberry_pi"),
}


def _node_meta(node_code: str):
    return _NODE_DEFAULTS.get(node_code, (node_code, "esp32"))


class Database:
    def __init__(self, dsn: str) -> None:
        self._dsn = dsn
        self._lock = threading.RLock()
        self._conn: Optional[psycopg.Connection] = None
        self._node_cache: Dict[str, int] = {}

    def connect(self) -> None:
        with self._lock:
            self._conn = psycopg.connect(self._dsn, autocommit=True, connect_timeout=10)
            log.info("connected to PostgreSQL")

    def _ensure(self) -> psycopg.Connection:
        if self._conn is None or self._conn.closed:
            self.connect()
        assert self._conn is not None
        return self._conn

    def close(self) -> None:
        with self._lock:
            if self._conn is not None and not self._conn.closed:
                self._conn.close()
            self._conn = None

    def resolve_node_id(self, node_code: str, auto_create: bool) -> Optional[int]:
        if node_code in self._node_cache:
            return self._node_cache[node_code]
        with self._lock:
            conn = self._ensure()
            with conn.cursor() as cur:
                cur.execute("SELECT id FROM esp_nodes WHERE node_code = %s", (node_code,))
                row = cur.fetchone()
                if row is None and auto_create:
                    name, ntype = _node_meta(node_code)
                    cur.execute(
                        "INSERT INTO esp_nodes (node_code, node_name, node_type, status, created_at) "
                        "VALUES (%s, %s, %s, 'offline', %s) RETURNING id",
                        (node_code, name, ntype, utcnow()),
                    )
                    row = cur.fetchone()
                    log.info("auto-created esp_nodes row for %s", node_code)
                if row is None:
                    return None
                self._node_cache[node_code] = int(row[0])
                return self._node_cache[node_code]

    def apply(self, write: DbWrite, auto_create: bool) -> None:
        node_id: Optional[int] = None
        if write.node_code:
            node_id = self.resolve_node_id(write.node_code, auto_create)
            if node_id is None:
                log.warning("unknown node '%s'; skipping write to %s", write.node_code, write.table)
                return

        values = dict(write.values)
        if write.table == ACCESS_LOGS and values.get("method") == "RFID" and not values.get("user_id"):
            uid = values.get("card_uid")
            if uid:
                values["user_id"] = self._lookup_user_by_card(uid)

        self._insert(write.table, node_id, values)

    def _insert(self, table: str, node_id: Optional[int], values: Dict[str, Any]) -> None:
        cols: List[str] = []
        params: List[Any] = []
        if node_id is not None:
            cols.append("node_id")
            params.append(node_id)
        for key, val in values.items():
            cols.append(key)
            params.append(val)
        cols.append("created_at")
        params.append(utcnow())

        placeholders = ", ".join("%s::jsonb" if c == "raw_payload" else "%s" for c in cols)
        sql = f"INSERT INTO {table} ({', '.join(cols)}) VALUES ({placeholders})"
        with self._lock:
            conn = self._ensure()
            with conn.cursor() as cur:
                cur.execute(sql, params)
        log.debug("inserted into %s (%s)", table, ", ".join(cols))

    def _lookup_user_by_card(self, uid: str) -> Optional[int]:
        try:
            with self._lock:
                conn = self._ensure()
                with conn.cursor() as cur:
                    cur.execute(
                        "SELECT user_id FROM rfid_cards WHERE card_uid = %s AND is_active = true "
                        "ORDER BY id DESC LIMIT 1",
                        (uid,),
                    )
                    row = cur.fetchone()
                    return int(row[0]) if row else None
        except Exception:
            log.debug("rfid_cards lookup failed for uid=%s", uid, exc_info=True)
            return None

    def update_presence(self, node_code: str, online: bool, raw_payload: Optional[str],
                        raise_offline: bool, auto_create: bool) -> None:
        node_id = self.resolve_node_id(node_code, auto_create)
        if node_id is None:
            log.warning("presence: unknown node '%s'", node_code)
            return
        now = utcnow()
        with self._lock:
            conn = self._ensure()
            with conn.cursor() as cur:
                if online:
                    cur.execute(
                        "UPDATE esp_nodes SET status='online', last_seen_at=%s, updated_at=%s WHERE id=%s",
                        (now, now, node_id),
                    )
                else:
                    cur.execute(
                        "UPDATE esp_nodes SET status='offline', updated_at=%s WHERE id=%s",
                        (now, node_id),
                    )
                    if raise_offline:
                        cur.execute(
                            "INSERT INTO alerts (alert_type, level, node_id, message, raw_payload, is_resolved, created_at) "
                            "VALUES ('DEVICE_OFFLINE', 'Critical', %s, %s, %s::jsonb, false, %s)",
                            (node_id, f"Node {node_code} mat ket noi.", raw_payload, now),
                        )
        log.info("presence: %s -> %s", node_code, "online" if online else "offline")
