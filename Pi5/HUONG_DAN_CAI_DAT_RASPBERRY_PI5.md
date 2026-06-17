# Hướng dẫn cài đặt Raspberry Pi 5 — Smart Home Hub (GĐ C, nền tảng)

> Tài liệu này liệt kê **mọi thứ cần cài trên Raspberry Pi 5** và hướng dẫn **chi tiết từng phần**
> để biến Pi thành "hub luôn-bật": **Mosquitto (MQTT broker)** + **PostgreSQL (database)** +
> **bridge MQTT→DB** (thư mục `Pi5/bridge/`).
>
> Phần cứng đã có: **Raspberry Pi 5 8GB**. Phần nhận diện khuôn mặt để **giai đoạn sau** (xem §11).
> Tổng quan kiến trúc & lý do: `../docs/PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md`.

---

## 0. Checklist tổng hợp những thứ cần cài

| # | Thành phần | Gói / công cụ | Mục đích | Mục |
|---|---|---|---|---|
| 1 | Hệ điều hành | Raspberry Pi OS 64-bit (Bookworm) | Nền tảng | §1 |
| 2 | Cập nhật + gói nền | `git curl openssl build-essential` | Tiện ích cơ bản | §2 |
| 3 | MQTT broker | `mosquitto mosquitto-clients` | Trung tâm nhắn tin IoT | §4 |
| 4 | TLS certificates | `openssl` (script `gen_certs.sh`) | Mã hoá MQTT (8883) | §4.3 |
| 5 | Database | `postgresql` (v15 trên Bookworm) | Lưu sensor/log/alert | §5 |
| 6 | Python + venv | `python3-venv python3-pip` | Chạy bridge | §6 |
| 7 | Thư viện Python | `paho-mqtt psycopg PyYAML python-dotenv` | Bridge MQTT→DB | §6.2 |
| 8 | Dịch vụ nền | `systemd` (có sẵn) | Bridge tự khởi động | §7 |
| 9 | *(sau)* Nhận diện mặt | `opencv, face_recognition, mediapipe, picamera2` | GĐ C bước 2 | §11 |

> **Đường tắt**: phần lớn các bước §2, §4.1–4.2, §6.1–6.2 đã được gói trong `scripts/setup_pi.sh`.
> Bạn có thể chạy script đó rồi quay lại đọc từng mục để hiểu/đối chiếu. Dưới đây là **bản thủ công
> chi tiết** cho từng phần.

---

## 1. Cài hệ điều hành & truy cập Pi

### 1.1. Phần cứng tối thiểu
- Raspberry Pi 5 8GB (đã có), nguồn **USB-C 27W chính hãng** (Pi 5 kén nguồn — nguồn yếu sẽ tụt áp).
- **Active Cooler** (gắn quạt) — Pi 5 chạy 24/7 + sau này nhận diện ảnh sẽ nóng.
- microSD ≥ 32GB (tốt nhất A2), **hoặc** SSD NVMe qua HAT (bền hơn cho ghi DB liên tục).

### 1.2. Flash Raspberry Pi OS
1. Cài **Raspberry Pi Imager** trên PC: https://www.raspberrypi.com/software/
2. Chọn: **Raspberry Pi 5** → OS = **Raspberry Pi OS (64-bit)** (bản Bookworm) → chọn thẻ nhớ.
3. Bấm **⚙ (Edit Settings)** trước khi ghi, đặt sẵn:
   - Hostname: `smarthome-hub` (sẽ truy cập `smarthome-hub.local`)
   - Bật **SSH** (Password authentication)
   - Username/password (vd `pi` / mật khẩu mạnh)
   - WiFi SSID + mật khẩu (nếu không cắm dây LAN), locale `Asia/Ho_Chi_Minh`
4. Ghi thẻ → cắm vào Pi → cấp nguồn.

### 1.3. SSH vào Pi
```bash
ssh pi@smarthome-hub.local
# nếu không resolve được .local thì dùng IP từ router, vd:
ssh pi@192.168.1.50
```

> **Ghi nhớ IP LAN của Pi** (vd `192.168.1.50`) — dùng xuyên suốt cho TLS cert (CN), cấu hình WPF,
> và firmware ESP32 sau này. Nên đặt **IP tĩnh** cho Pi trong router (DHCP reservation).

---

## 2. Cập nhật hệ thống & gói nền

```bash
sudo apt update && sudo apt full-upgrade -y
sudo apt install -y git curl openssl build-essential
sudo reboot      # nếu kernel/firmware vừa được cập nhật
```

---

## 3. Lấy mã nguồn về Pi

```bash
mkdir -p ~/smarthome && cd ~/smarthome
# Repo công khai:
git clone https://github.com/NQA14112004/Smart_Home_Prj.git .
# (Repo private: tạo Personal Access Token và clone qua HTTPS, hoặc dùng scp từ PC.)

cd ~/smarthome/Pi5     # toàn bộ lệnh dưới chạy trong thư mục này
```

> Hoặc copy thẳng từ PC Windows: `scp -r C:\Project\Pi5 pi@192.168.1.50:~/smarthome/Pi5`

---

## 4. Cài & cấu hình Mosquitto (MQTT broker)

### 4.1. Cài
```bash
sudo apt install -y mosquitto mosquitto-clients
sudo systemctl enable mosquitto
```

### 4.2. Tạo người dùng MQTT (bắt buộc — broker bật auth)
Mỗi thiết bị một tài khoản riêng để dễ thu hồi:
```bash
# Lần đầu dùng -c để TẠO file; các lần sau BỎ -c (nếu không sẽ ghi đè).
sudo mosquitto_passwd -c /etc/mosquitto/passwd bridge      # cho bridge (bắt buộc)
sudo mosquitto_passwd    /etc/mosquitto/passwd wpfclient   # cho app WPF
sudo mosquitto_passwd    /etc/mosquitto/passwd esp32-door  # node cửa (GĐ B)
sudo mosquitto_passwd    /etc/mosquitto/passwd esp32-home  # node nhà (GĐ B)
```
> Ghi lại mật khẩu `bridge` — sẽ điền vào `Pi5/.env` ở §6.3.

### 4.3. Tạo chứng chỉ TLS (mã hoá cổng 8883)
Dùng script kèm sẵn, **CN = IP LAN của Pi** (clients sẽ kết nối tới địa chỉ này):
```bash
./scripts/gen_certs.sh 192.168.1.50      # thay bằng IP/hostname thật của Pi
sudo mkdir -p /etc/mosquitto/certs
sudo cp certs/ca.crt certs/server.crt certs/server.key /etc/mosquitto/certs/
sudo chown mosquitto: /etc/mosquitto/certs/*
sudo chmod 640 /etc/mosquitto/certs/server.key
```
> File `certs/ca.crt` cần copy sang **mọi client**: app WPF (`MqttOptions.CaCertPath`), bridge
> (`config.yaml: mqtt.ca_cert`), và firmware ESP32 sau này.

### 4.4. Áp cấu hình broker cho Pi
```bash
sudo cp mosquitto/mosquitto.pi.conf /etc/mosquitto/conf.d/smarthome.conf
sudo systemctl restart mosquitto
sudo systemctl status mosquitto --no-pager     # phải thấy active (running)
```
Cấu hình này (xem `mosquitto/mosquitto.pi.conf`): `allow_anonymous false`, listener **8883 TLS** mở ra
LAN, kèm listener 1883 chỉ-localhost để debug, và bật persistence.

### 4.5. Kiểm tra Mosquitto
Mở 2 phiên SSH:
```bash
# Phiên 1 — lắng nghe (dùng TLS + CA):
mosquitto_sub -h 192.168.1.50 -p 8883 --cafile certs/ca.crt \
  -u bridge -P 'MAT_KHAU_BRIDGE' -t 'smarthome/#' -v

# Phiên 2 — gửi thử:
mosquitto_pub -h 192.168.1.50 -p 8883 --cafile certs/ca.crt \
  -u bridge -P 'MAT_KHAU_BRIDGE' -t 'smarthome/test' -m 'hello'
```
Phiên 1 hiện `smarthome/test hello` là OK.

---

## 5. Cài & cấu hình PostgreSQL (database)

### 5.1. Cài
```bash
sudo apt install -y postgresql
sudo systemctl enable postgresql
```

### 5.2. Tạo role + database
```bash
sudo -u postgres psql
```
Trong psql, chạy (đổi `CHANGE_ME` thành mật khẩu mạnh):
```sql
CREATE ROLE app WITH LOGIN PASSWORD 'CHANGE_ME';
CREATE DATABASE "Smart_Home_db" OWNER app;
\q
```

### 5.3. Đưa schema từ PC sang Pi
Trên **PC Windows** (nơi đang có DB), xuất schema:
```powershell
pg_dump --schema-only --no-owner --no-privileges -U postgres Smart_Home_db > smarthome_schema.sql
```
> Việc này cũng khắc phục file `Smart_home_security_database.sql` đang **0 byte** trong repo —
> commit bản schema mới này lại.

Copy file sang Pi rồi nạp:
```bash
# trên PC:
scp smarthome_schema.sql pi@192.168.1.50:~/smarthome/Pi5/
# trên Pi:
psql -U app -d Smart_Home_db -h 127.0.0.1 -f ~/smarthome/Pi5/smarthome_schema.sql
```
Chi tiết đầy đủ (kể cả dump cả dữ liệu): `scripts/migrate_db.md`.

### 5.4. (Tuỳ chọn) Cho phép WPF trên PC truy cập DB qua LAN
Chỉ làm nếu app WPF cần nối thẳng vào DB trên Pi. Sửa 2 file (đường dẫn theo Postgres 15):
- `/etc/postgresql/15/main/postgresql.conf` → đặt `listen_addresses = '*'`
- `/etc/postgresql/15/main/pg_hba.conf` → thêm dòng (đổi subnet cho đúng LAN):
  `host  Smart_Home_db  app  192.168.1.0/24  scram-sha-256`

Rồi: `sudo systemctl restart postgresql`

### 5.5. Kiểm tra
```bash
psql -U app -d Smart_Home_db -h 127.0.0.1 -c "\dt"     # phải liệt kê các bảng đã nạp
```

---

## 6. Cài Python + bridge MQTT→DB

### 6.1. Tạo virtualenv
Raspberry Pi OS Bookworm chặn `pip` cài toàn cục (PEP 668) → **bắt buộc dùng venv**:
```bash
cd ~/smarthome/Pi5
sudo apt install -y python3-venv python3-pip
python3 -m venv .venv
./.venv/bin/pip install --upgrade pip
```

### 6.2. Cài thư viện
```bash
./.venv/bin/pip install -r requirements.txt           # chạy bridge
./.venv/bin/pip install -r requirements-dev.txt        # + pytest để test
```

### 6.3. Cấu hình
```bash
cp .env.example .env
cp config.example.yaml config.yaml
nano .env          # điền DATABASE_URL, MQTT_USERNAME=bridge, MQTT_PASSWORD=...
nano config.yaml   # kiểm tra mqtt.host (IP Pi), mqtt.ca_cert trỏ /etc/mosquitto/certs/ca.crt
```
`.env` mẫu:
```
DATABASE_URL=postgresql://app:CHANGE_ME@127.0.0.1:5432/Smart_Home_db
MQTT_USERNAME=bridge
MQTT_PASSWORD=MAT_KHAU_BRIDGE
```
> `.env` và `config.yaml` **không bao giờ commit** (đã có trong `.gitignore`).

### 6.4. Chạy unit test (không cần DB/broker)
```bash
./.venv/bin/python -m pytest -q
```

### 6.5. Chạy thử bridge + bơm dữ liệu giả
```bash
# Phiên 1 — chạy bridge:
./.venv/bin/python -m bridge.main

# Phiên 2 — bơm message giả mọi topic (không cần ESP32/camera):
./.venv/bin/python -m tools.simulate
# kèm cờ --offline để thử cả tình huống node mất kết nối:
./.venv/bin/python -m tools.simulate --offline
```
Kiểm tra dữ liệu đã ghi vào DB:
```bash
psql -U app -d Smart_Home_db -h 127.0.0.1 \
  -c "SELECT created_at, temperature, gas_value FROM sensor_readings ORDER BY created_at DESC LIMIT 5;" \
  -c "SELECT created_at, method, result FROM access_logs ORDER BY created_at DESC LIMIT 5;" \
  -c "SELECT created_at, alert_type, level FROM alerts ORDER BY created_at DESC LIMIT 5;"
```

---

## 7. Chạy bridge như dịch vụ tự khởi động (systemd)

```bash
# Sửa User= và đường dẫn trong unit nếu khác (mặc định: user 'pi', /home/pi/smarthome/Pi5):
nano systemd/smarthome-bridge.service

sudo cp systemd/smarthome-bridge.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now smarthome-bridge

sudo systemctl status smarthome-bridge --no-pager
journalctl -u smarthome-bridge -f       # xem log realtime
```
Từ giờ bridge tự chạy khi Pi khởi động và tự restart nếu lỗi.

---

## 8. Trỏ app WPF (trên PC) sang Pi

Trong cấu hình WPF (`.env` / user-secrets — KHÔNG sửa `appsettings.json` commit):
- **Database**: `Host=192.168.1.50;Database=Smart_Home_db;Username=app;Password=CHANGE_ME`
- **MQTT**: `Host=192.168.1.50`, `Port=8883`, `UseTls=true`, `CaCertPath=<đường dẫn ca.crt copy về PC>`,
  `Username=wpfclient`, `Password=...`

> ⚠️ **Tránh ghi trùng**: bridge giờ là writer luôn-bật cho `esp_nodes`/`DEVICE_OFFLINE`. Nếu để WPF
> mở cùng lúc, `NodePresenceService` của WPF sẽ ghi đôi. Khi đã chuyển hẳn sang Pi-hub, nên tắt phần
> **ghi-DB** của `NodePresenceService` (giữ phần cập nhật UI). Chi tiết: `Pi5/README.md` mục "Tránh ghi trùng".

---

## 9. Kiểm tra toàn hệ thống (smoke test)

1. `systemctl status mosquitto postgresql smarthome-bridge` → cả 3 **active (running)**.
2. `python -m tools.simulate` → xuất hiện bản ghi mới trong `sensor_readings`/`access_logs`/`alerts`.
3. Mở Dashboard WPF (đã trỏ sang Pi) → thấy nhiệt độ/độ ẩm/gas và cảnh báo mới nhất.
4. `tools.simulate --offline` → trong `esp_nodes`, node chuyển `offline`; có alert `DEVICE_OFFLINE`.

---

## 10. Xử lý sự cố thường gặp

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `mosquitto` không lên | Sai cú pháp config / thiếu cert | `sudo mosquitto -c /etc/mosquitto/conf.d/smarthome.conf -v` để xem lỗi |
| Client TLS báo "certificate verify failed" | CN cert ≠ địa chỉ kết nối | Tạo lại cert với đúng IP/hostname (§4.3) |
| `mosquitto_sub` báo "Connection refused" | Sai user/pass hoặc chưa mở listener LAN | Kiểm tra `/etc/mosquitto/passwd` và listener 8883 |
| Bridge: `Missing required environment variable: DATABASE_URL` | Chưa tạo/điền `.env` | `cp .env.example .env` rồi điền |
| Bridge: `connection refused` tới DB | Sai DSN / Postgres chưa chạy | Kiểm tra `DATABASE_URL`, `systemctl status postgresql` |
| `pip install` báo "externally-managed-environment" | Cài ngoài venv (PEP 668) | Luôn dùng `./.venv/bin/pip` |
| Bảng không có dữ liệu sau simulate | Bridge chưa chạy / sai topic | Xem `journalctl -u smarthome-bridge -f` khi bơm |

---

## 11. (Giai đoạn sau) Chuẩn bị cho nhận diện khuôn mặt

Khi làm bước nhận diện khuôn mặt (publish `smarthome/face/result` — bridge đã sẵn sàng nhận), sẽ cần:
```bash
# Camera (Camera Module 3 hoặc webcam USB) — picamera2 thường đã có sẵn trên Pi OS:
sudo apt install -y python3-picamera2 libcamera-apps

# Thị giác máy tính (CPU-only):
sudo apt install -y python3-opencv cmake libopenblas-dev   # cmake/openblas để build dlib
./.venv/bin/pip install face_recognition mediapipe         # dlib biên dịch lâu (~15–30 phút trên Pi 5)
```
> Lưu ý: `face_recognition` kéo theo `dlib` phải **biên dịch** trên Pi (mất thời gian + RAM) — nên làm
> khi có thời gian, có active cooler, và nguồn ổn định. Chi tiết stack & lý do: Phần 3 của lộ trình.

---

## Phụ lục — Bảo mật & sao lưu

- **Mật khẩu**: đặt mạnh cho `app` (Postgres), các user MQTT, và tài khoản hệ điều hành. Không commit `.env`.
- **Sao lưu DB định kỳ**: `pg_dump -U app Smart_Home_db | gzip > backup.sql.gz` (chạy bằng cron, đặt tên theo ngày).
- **Cập nhật**: `sudo apt update && sudo apt full-upgrade` định kỳ; theo dõi bản vá Mosquitto/Postgres.
- **Tường lửa (tuỳ chọn)**: chỉ mở 8883 (MQTT TLS) và 5432 (nếu WPF cần) ra LAN; cân nhắc VLAN IoT.
