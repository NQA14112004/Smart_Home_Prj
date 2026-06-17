# Pi 5 Hub — GĐ C (nền tảng): Mosquitto + Bridge MQTT→DB

Phần phần mềm chạy trên **Raspberry Pi 5** làm "hub luôn-bật" của hệ thống Smart Home.
Đợt này triển khai **nền tảng**: broker Mosquitto (auth + TLS) và **bridge MQTT→PostgreSQL** —
thành phần ghi dữ liệu luôn-bật mà app WPF còn thiếu. Nhận diện khuôn mặt là bước kế tiếp.

> Bối cảnh & lộ trình đầy đủ: `../Smart_Home/PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md` (Phần 5, GĐ C).

## Bridge làm gì

Subscribe các topic `smarthome/...` (xem `../Smart_Home/Services/MqttTopics.cs`) và ghi vào **cùng
schema PostgreSQL mà WPF đọc**:

| Topic vào | Bảng ghi | Ghi chú |
|---|---|---|
| `smarthome/sensor/home` | `sensor_readings` (esp32-home) | temperature/humidity/gas_value/light_value |
| `smarthome/status/door` | `sensor_readings` (esp32-door) | door_status/lock_status |
| `smarthome/door/rfid` | `access_logs` (method=RFID) | map card_uid→user_id (best-effort) |
| `smarthome/door/keypad` | `access_logs` (method=PIN) | |
| `smarthome/face/result` | `access_logs` (method=FACE_RECOGNITION) | do module nhận diện publish |
| `smarthome/door/breach` | `alerts` (FORCED_ENTRY, critical) | |
| `smarthome/alarm/gas` | `alerts` (GAS_HIGH) | level theo ngưỡng gas |
| `smarthome/status/{node}/online` | `esp_nodes` + `alerts` (DEVICE_OFFLINE) | LWT + heartbeat |

Dashboard WPF đọc `esp_nodes`, `sensor_readings` mới nhất và `alerts` → khi bridge ghi, Dashboard
hiển thị dữ liệu thật (sau khi refresh/khởi động lại view).

## Kiến trúc thư mục

```
Pi5/
├── bridge/            # package Python
│   ├── handlers.py    # MQTT payload -> DbWrite (THUẦN, không I/O -> dễ test)
│   ├── db.py          # psycopg3: resolve node, INSERT, presence, map RFID->user
│   ├── presence.py    # online/offline (port của NodePresenceService.cs)
│   ├── mqtt_client.py # paho: TLS, auth, auto-reconnect, subscribe, dispatch
│   ├── config.py      # nạp .env (secrets) + config.yaml (layout)
│   └── main.py        # entrypoint: python -m bridge.main
├── tests/             # pytest cho handlers (không cần DB/broker)
├── tools/simulate.py  # publish message giả để test end-to-end (không cần ESP32/camera)
├── mosquitto/         # config broker cho Pi (auth + TLS + LAN)
├── systemd/           # unit chạy bridge như service
└── scripts/           # setup_pi.sh, gen_certs.sh, migrate_db.md
```

## Triển khai (tóm tắt)

```bash
# Trên Pi 5, sau khi copy thư mục repo sang (vd ~/smarthome):
cd ~/smarthome/Pi5
./scripts/setup_pi.sh                 # cài mosquitto + postgresql + python venv + tạo user MQTT
./scripts/gen_certs.sh 192.168.1.10   # IP LAN của Pi -> tạo cert TLS tự ký
sudo cp certs/ca.crt certs/server.crt certs/server.key /etc/mosquitto/certs/
sudo cp mosquitto/mosquitto.pi.conf /etc/mosquitto/conf.d/smarthome.conf
sudo systemctl restart mosquitto

# Database: làm theo scripts/migrate_db.md (dump trên PC -> restore trên Pi)

cp .env.example .env                  # điền DATABASE_URL + MQTT_USERNAME/PASSWORD
cp config.example.yaml config.yaml    # chỉnh host/ca_cert nếu cần

# Chạy thử:
./.venv/bin/python -m bridge.main
# Cửa sổ khác:
./.venv/bin/python -m tools.simulate  # bơm message giả -> kiểm tra bảng trong DB
```

Cài như service luôn-bật: xem `systemd/smarthome-bridge.service`.

## Kiểm thử

```bash
# Unit test (thuần, không cần DB/broker):
./.venv/bin/pip install -r requirements-dev.txt
./.venv/bin/python -m pytest -q

# End-to-end: bật bridge rồi chạy simulate, sau đó:
psql -U app -d Smart_Home_db -c "SELECT created_at, table_name FROM sensor_readings ORDER BY created_at DESC LIMIT 5;"
```

## Tránh ghi trùng (quan trọng)

App WPF có `NodePresenceService` cũng cập nhật `esp_nodes` + tạo `DEVICE_OFFLINE` từ LWT. Khi bridge
trên Pi đã là writer luôn-bật, nếu WPF cũng mở thì **presence bị ghi đôi**. Khuyến nghị: khi chuyển
hẳn sang kiến trúc Pi-hub, tắt phần ghi-DB của `NodePresenceService` (giữ phần cập nhật UI), để bridge
là nguồn chân lý duy nhất. Các bảng `sensor_readings`/`access_logs`/`alerts` thì hiện **chỉ bridge ghi**
(WPF không ghi) nên không trùng.

## Bảo mật

- Secrets (`DATABASE_URL`, `MQTT_*`) chỉ nằm trong `.env` (đã gitignore). `config.yaml` chỉ chứa layout.
- Mosquitto bật `allow_anonymous false` + TLS (port 8883). Tạo user MQTT riêng cho từng node.
- Bridge đặt LWT `smarthome/status/bridge/online` để client khác biết bridge sống/chết.
