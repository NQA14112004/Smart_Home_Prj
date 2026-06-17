# Smart Home Security System

Hệ thống an ninh nhà thông minh gồm **ứng dụng quản trị WPF trên Windows** (đã hoàn thiện), kết nối với các node phần cứng **ESP32** và **Raspberry Pi 5** (nhận diện khuôn mặt) qua **MQTT** — phần thiết bị đang trong lộ trình phát triển.

> 📄 Tài liệu chi tiết:
> - [`docs/PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md`](docs/PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md) — phân tích codebase, kiến trúc đề xuất, sơ đồ chân ESP32, lộ trình & chi phí.
> - [`SECURITY.md`](SECURITY.md) — quản lý secrets, cấu hình MQTT auth/TLS, các việc bảo mật còn tồn đọng.

---

## 1. Tổng quan kiến trúc

```text
        ┌──────────────────── Mạng nội bộ (LAN) ────────────────────┐
        │                                                           │
  ┌─────┴─────┐      MQTT       ┌──────────────────────┐            │
  │ ESP32 CỬA │ ───────────────►│  HUB (hiện: PC dev,  │◄── MQTT ───┐│
  │ RFID/Keypad│                │  tương lai: Pi 5)     │           ││
  │ Khóa/Còi  │◄────────────────│  • Mosquitto broker  │   ┌───────┴┴──┐
  └───────────┘                 │  • PostgreSQL        │   │ ESP32 NHÀ │
       (GĐ B - chưa làm)        │  • Nhận diện mặt (CV)│   │ DHT22/MQ-2│
                                └──────────┬───────────┘   │ Đèn/Quạt  │
                                           │               └───────────┘
                                    ┌──────┴───────┐        (GĐ B - chưa làm)
                                    │ App WPF (UI) │  ✅ ĐÃ HOÀN THIỆN
                                    └──────────────┘
```

**Trạng thái hiện tại (2026-06-11):**

| Thành phần | Trạng thái |
|---|---|
| App WPF desktop (.NET 8) | ✅ Hoàn thiện (~90%), 47/47 unit test PASS |
| Service layer + DI + test coverage | ✅ Xong (95.4% line / 85.8% branch lớp service) |
| Bảo mật: secrets ra `.env`, MQTT auth | ✅ Xong (broker bind `127.0.0.1`, auth bắt buộc) |
| Firmware ESP32 (node cửa + node nhà) | 🔜 **Tiếp theo (GĐ B)** — đã có thiết kế chân + cấu trúc file |
| Raspberry Pi 5 hub + nhận diện khuôn mặt | ⏳ Chưa bắt đầu (GĐ C) |
| Cảnh báo cạy/phá cửa (sensor fusion) | ⏳ Chưa bắt đầu (GĐ D) |

---

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ | Vai trò |
|---|---|---|
| UI desktop | **WPF, .NET 8** (`net8.0-windows`) | Dashboard quản trị |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | `ObservableObject`, `[ObservableProperty]`, `RelayCommand` |
| Database | PostgreSQL + EF Core (Npgsql 8.0.2) | Code-first, `IDbContextFactory` |
| MQTT | MQTTnet 4.3.7 (ManagedClient) | Auto-reconnect, QoS 1, hỗ trợ TLS + auth |
| Biểu đồ | LiveChartsCore.SkiaSharpView.WPF | Nhiệt độ/độ ẩm realtime |
| Bảo mật | BCrypt.Net-Next | Hash mã PIN (không lưu plaintext) |
| Broker | Eclipse Mosquitto (self-host) | `allow_anonymous false` + password file |
| Test | xUnit + EF Core InMemory + coverlet | 47 unit test cho service layer |

---

## 3. Cấu trúc dự án & chức năng từng phần

```text
C:\Project
├── Smart_Home.slnx                  # Solution (Smart_Home + Smart_Home.Tests)
├── README.md                        # Tài liệu tổng quan (file này)
├── SECURITY.md                      # Hướng dẫn secrets, MQTT auth/TLS
├── docs/
│   └── PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md  # Phân tích codebase & lộ trình toàn hệ thống
├── database/
│   ├── README.md                    # Nguồn schema & cách dump lại
│   └── Smart_home_security_database.sql     # Schema PostgreSQL (0 byte — cần pg_dump lại)
├── infra/
│   └── broker-setup/                # Cấu hình Mosquitto cho máy DEV (PC Windows)
│       └── auth.conf                # allow_anonymous false, password_file, listener 127.0.0.1:1883
│                                    #   (passwd đã gỡ khỏi git + gitignore)
├── Smart_Home/                      # ===== APP WPF CHÍNH =====
│   ├── App.xaml / App.xaml.cs       # Khởi động: nạp .env → build IConfiguration →
│   │                                #   đăng ký DI (DbContextFactory, services, ViewModels)
│   ├── EnvFileLoader.cs             # Nạp file .env vào biến môi trường tiến trình
│   │                                #   (env thật luôn ưu tiên hơn .env)
│   ├── .env.example                 # Template credential (DB + MQTT) — copy thành .env
│   ├── appsettings.json             # Cấu hình không nhạy cảm (Password để trống)
│   ├── MainWindow.xaml(.cs)         # Cửa sổ chính, điều hướng giữa các View
│   │
│   ├── Data/
│   │   └── AppDbContext.cs          # EF Core DbContext — 12 DbSet ánh xạ schema PostgreSQL
│   │
│   ├── Models/                      # Entity + options
│   │   ├── User.cs / Role.cs        # Người dùng + vai trò (soft-delete giữ vết audit)
│   │   ├── RfidCard.cs / PinCode.cs # Thẻ RFID & mã PIN (PIN hash BCrypt)
│   │   ├── AccessLog.cs             # Lịch sử ra vào (method: RFID/PIN/FACE..., raw_payload JSONB)
│   │   ├── Alert.cs                 # Cảnh báo (GAS_HIGH, DEVICE_OFFLINE, FORCED_ENTRY...)
│   │   ├── Device.cs / DeviceCommand.cs / DeviceStatusLog.cs  # Thiết bị & lệnh điều khiển
│   │   ├── EspNode.cs               # Node phần cứng (esp32 / raspberry_pi) + trạng thái online
│   │   ├── SensorReading.cs         # Dữ liệu cảm biến (nhiệt độ, độ ẩm, gas)
│   │   ├── SystemSetting.cs         # Cấu hình hệ thống lưu DB
│   │   ├── MqttOptions.cs           # Tùy chọn MQTT (Host/Port/TLS/CaCertPath/credential)
│   │   ├── NodePresenceOptions.cs   # Tùy chọn theo dõi heartbeat node
│   │   └── MqttTopic.cs
│   │
│   ├── Services/                    # ===== SERVICE LAYER (business logic) =====
│   │   ├── UserService.cs           # CRUD người dùng, khóa/mở tài khoản
│   │   ├── RfidCardService.cs       # CRUD + gán/thu hồi thẻ RFID
│   │   ├── PinCodeService.cs        # CRUD mã PIN (hash qua IPasswordHasher)
│   │   ├── PasswordHasher.cs        # Bọc BCrypt — hash & verify PIN
│   │   ├── DeviceService.cs         # Quản lý thiết bị, gửi lệnh điều khiển
│   │   ├── AccessLogService.cs      # Ghi & truy vấn lịch sử ra vào (lọc theo ngày/phương thức)
│   │   ├── AlertService.cs          # Truy vấn & đánh dấu xử lý cảnh báo
│   │   ├── DashboardService.cs      # Tổng hợp số liệu cho Dashboard
│   │   ├── OperationResult.cs       # Kiểu trả về chung (thành công/lỗi + thông điệp)
│   │   ├── IMqttService.cs          # Interface MQTT client
│   │   ├── MqttClientService.cs     # MQTTnet ManagedClient: auto-reconnect, auth,
│   │   │                            #   TLS pin theo CA self-signed (CustomRootTrust)
│   │   ├── MqttTopics.cs            # ★ NGUỒN CHÂN LÝ DUY NHẤT cho tên topic (prefix smarthome/)
│   │   └── NodePresenceService.cs   # Theo dõi LWT + heartbeat → phát DEVICE_OFFLINE
│   │                                #   đúng 1 lần khi node online→offline
│   │
│   ├── ViewModels/                  # ===== MVVM — chỉ gọi service, không chứa EF Core =====
│   │   ├── MainViewModel.cs         # Điều hướng tab/màn hình
│   │   ├── DashboardViewModel.cs    # Subscribe MQTT realtime, biểu đồ nhiệt/ẩm, số liệu tổng
│   │   ├── UsersViewModel.cs        # Màn hình quản lý người dùng
│   │   ├── RFIDCardsViewModel.cs    # Màn hình quản lý thẻ RFID
│   │   ├── PinCodesViewModel.cs     # Màn hình quản lý mã PIN
│   │   ├── AccessLogsViewModel.cs   # Lịch sử ra vào, lọc theo khoảng ngày
│   │   ├── AlertsViewModel.cs       # Danh sách cảnh báo + đánh dấu đã xử lý
│   │   └── DeviceControlViewModel.cs# Bật/tắt thiết bị → publish smarthome/{device_code}/set
│   │
│   ├── Views/                       # XAML tương ứng 1-1 với ViewModel
│   │   ├── DashboardView.xaml       # Tổng quan: trạng thái cửa, sensor, biểu đồ, node online
│   │   ├── UsersView.xaml           # CRUD người dùng
│   │   ├── RFIDCardsView.xaml       # CRUD thẻ RFID
│   │   ├── PinCodesView.xaml        # CRUD mã PIN
│   │   ├── AccessLogsView.xaml      # Bảng lịch sử ra vào
│   │   ├── AlertsView.xaml          # Bảng cảnh báo
│   │   └── DeviceControlView.xaml   # Điều khiển đèn/quạt/khóa
│   │
│   └── Helpers/
│       └── BoolToColorConverter.cs  # Converter XAML (trạng thái → màu)
│
├── Smart_Home.Tests/                # ===== UNIT TEST (xUnit + EF InMemory) =====
│   ├── CrudServiceTests.cs          # Test User/RFID/PIN service
│   ├── DeviceAndQueryServiceTests.cs# Test Device/AccessLog/Alert/Dashboard service
│   ├── UnitTests.cs                 # Test PasswordHasher, OperationResult, helpers
│   ├── TestSupport.cs               # Hạ tầng test chung (factory InMemory DB)
│   └── coverlet.runsettings         # Cấu hình đo coverage
│
└── Pi5/                             # ===== HUB RASPBERRY PI 5 (GĐ C) — bridge MQTT→PostgreSQL =====
    ├── bridge/                      # Package Python: handlers, db, presence, mqtt_client, config, main
    ├── tests/                       # pytest cho handlers (thuần, không cần DB/broker)
    ├── tools/simulate.py            # Bơm message giả test end-to-end (không cần phần cứng)
    ├── mosquitto/                   # Cấu hình broker cho Pi (auth + TLS + LAN)
    ├── systemd/                     # Unit chạy bridge như service luôn-bật
    ├── scripts/                     # setup_pi.sh, gen_certs.sh, migrate_db.md
    └── README.md                    # Hướng dẫn riêng cho Pi 5
```

> **Lưu ý quy ước**: thư mục là `Views/`, `Services/` (số nhiều) nhưng namespace giữ `Smart_Home.View`, `Smart_Home.Service` (số ít) — **có chủ đích** để tránh sửa hàng loạt `x:Class`/`clr-namespace` trong XAML. Đừng "sửa" điều này.

---

## 4. Chức năng từng màn hình (đã hoàn thiện)

| Màn hình | Chức năng |
|---|---|
| **Dashboard** | Trạng thái cửa/khóa, nhiệt độ/độ ẩm realtime qua MQTT, biểu đồ LiveCharts, trạng thái online/offline từng node, số liệu tổng hợp |
| **Users** | Thêm/sửa/xóa (soft-delete) người dùng, gán vai trò, khóa tài khoản |
| **RFID Cards** | Quản lý thẻ, gán thẻ cho người dùng, vô hiệu hóa thẻ |
| **PIN Codes** | Quản lý mã PIN (hash BCrypt, không lưu plaintext) |
| **Access Logs** | Lịch sử ra vào theo phương thức (RFID/PIN/...), lọc theo khoảng ngày, lưu `raw_payload` JSONB phục vụ audit |
| **Alerts** | Danh sách cảnh báo theo mức độ, đánh dấu đã xử lý |
| **Device Control** | Bật/tắt đèn/quạt/khóa từ xa — publish lệnh MQTT, có hàng đợi khi mất kết nối |

---

## 5. Quy ước MQTT topic (prefix `smarthome/`)

Toàn bộ tên topic định nghĩa tập trung tại `Smart_Home/Services/MqttTopics.cs`.

| Topic | Hướng | Payload ví dụ |
|---|---|---|
| `smarthome/status/door` | ESP32 cửa → | `{"door":"closed","lock":"locked"}` |
| `smarthome/door/rfid` | ESP32 cửa → | `{"uid":"A1B2C3","result":"success"}` |
| `smarthome/door/keypad` | ESP32 cửa → | `{"result":"failed","attempts":3}` |
| `smarthome/door/breach` | ESP32 cửa → | `{"type":"FORCED_ENTRY","accel":2.1}` |
| `smarthome/door/control` | → ESP32 cửa | `{"command":"unlock","duration":5}` |
| `smarthome/sensor/home` | ESP32 nhà → | `{"temperature":30.5,"humidity":72,"gas":380}` |
| `smarthome/{device_code}/set` | → ESP32 nhà | `{"command":"turn_on"}` |
| `smarthome/face/result` | Pi 5 → | `{"userId":1,"confidence":0.9,"live":true}` |
| `smarthome/status/{node}/online` | node → (LWT, retained) | birth/last-will cho presence |

---

## 6. Cài đặt & chạy

### Yêu cầu
- Windows + .NET 8 SDK
- PostgreSQL (database `Smart_Home_db`)
- Eclipse Mosquitto (broker MQTT local)

### Các bước

```powershell
# 1. Cấu hình secrets — copy template rồi điền credential thật
cd Smart_Home
copy .env.example .env
# Sửa .env: SMART_HOME_CONNECTION_STRING, MQTT_USERNAME, MQTT_PASSWORD
# (Hoặc dùng dotnet user-secrets — xem SECURITY.md §2)

# 2. Chạy Mosquitto với auth bắt buộc (cấu hình mẫu trong infra/broker-setup/auth.conf)
#    Tạo user: mosquitto_passwd -c <đường-dẫn-passwd> wpfclient

# 3. Build & chạy app
dotnet build
dotnet run --project Smart_Home

# 4. Chạy test
dotnet test
```

Thứ tự ưu tiên đọc cấu hình: **biến môi trường / `.env` → user-secrets → appsettings.json**.

---

## 7. Lộ trình phát triển

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| **GĐ A** | Vá codebase + bảo mật vận hành (topic, secrets, MQTT auth) | ✅ Hoàn thành 2026-06-11 |
| **GĐ B** | Firmware 2 node ESP32 (cửa: RFID/keypad/khóa/còi · nhà: DHT22/MQ-2/relay) | 🔜 **Tiếp theo** |
| **GĐ C** | Pi 5 làm hub luôn-bật: Mosquitto + DB + nhận diện khuôn mặt (CPU-only) + bật TLS | ⏳ |
| **GĐ D** | Cảnh báo cạy/phá cửa: sensor fusion (reed + SW-420 + MPU6050) trên ESP32, còi cục bộ <100 ms | ⏳ |
| **GĐ E** | Nâng cấp tùy chọn (service layer ✅, 47 test ✅, node presence ✅; còn liveness nâng cao, push notification) | 🟡 Một phần |

Chi tiết từng giai đoạn (việc cần làm, tiêu chí hoàn thành, BOM linh kiện ~170–250 USD, sơ đồ chân GPIO, cấu trúc firmware): xem [`PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md`](docs/PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md) Phần 5–7.

---

## 8. Bảo mật — việc còn tồn đọng ⚠️

Xem chi tiết tại [`SECURITY.md`](SECURITY.md). Tóm tắt các việc **bắt buộc làm**:

1. **Xoay vòng mật khẩu PostgreSQL cũ** — bản cũ của SECURITY.md chứa mật khẩu dạng chữ và đã được push lên GitHub (`ALTER ROLE postgres WITH PASSWORD '<mới>'` rồi cập nhật `.env`).
2. **Đổi mật khẩu MQTT** — `infra/broker-setup/passwd` **đã được gỡ khỏi git + gitignore** (chuẩn hoá 2026-06-17); vì hash cũ đã push lên GitHub, vẫn cần đổi mật khẩu: `mosquitto_passwd -c "C:\Program Files\Mosquitto\passwd" wpfclient` rồi cập nhật `.env`.
3. **Khôi phục schema** `database/Smart_home_security_database.sql` (đang 0 byte): `pg_dump --schema-only Smart_Home_db > database/Smart_home_security_database.sql` (xem `database/README.md`).
4. **Bật TLS trên broker** khi mở listener ra LAN / chuyển broker lên Pi 5 (hướng dẫn ở SECURITY.md §4).

**Đã làm**: secrets ra khỏi source (`.env` + `EnvFileLoader`), Mosquitto auth bắt buộc bind localhost, PIN hash BCrypt, client MQTT hỗ trợ TLS + pin CA self-signed; chuẩn hoá cấu trúc repo (`docs/`, `database/`, `infra/broker-setup/`) + gỡ `passwd`/`TestResults/` khỏi git (2026-06-17).
