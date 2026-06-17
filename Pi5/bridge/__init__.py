"""Smart Home MQTT -> PostgreSQL bridge (runs on the Raspberry Pi 5 hub).

This package is the always-on writer that the WPF desktop app is missing: it
subscribes to the `smarthome/...` MQTT topics published by the ESP32 nodes (and
the Pi 5 face-recognition module) and persists them into the same PostgreSQL
schema the WPF app reads from (sensor_readings, access_logs, alerts, esp_nodes).

Topic and column conventions mirror:
  - Smart_Home/Services/MqttTopics.cs  (topic names)
  - Smart_Home/Models/*.cs             (table/column names)
  - PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md, Phụ lục A (payload shapes)
"""

__version__ = "0.1.0"
