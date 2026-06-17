"""Small shared helpers: UTC time + lenient value coercion.

Kept dependency-free so handlers.py stays pure and trivially unit-testable.
"""
from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Optional


def utcnow() -> datetime:
    """Naive UTC timestamp, matching EF Core's `DateTime.UtcNow` (Kind=Utc, no tzinfo)."""
    return datetime.now(timezone.utc).replace(tzinfo=None)


def as_float(value: Any) -> Optional[float]:
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def as_int(value: Any) -> Optional[int]:
    if value is None:
        return None
    try:
        return int(float(value))  # tolerate "380" and 380.0
    except (TypeError, ValueError):
        return None


def as_bool(value: Any) -> Optional[bool]:
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    if isinstance(value, str):
        return value.strip().lower() in ("1", "true", "yes", "on")
    return None


def as_str(value: Any) -> Optional[str]:
    if value is None:
        return None
    return str(value)
