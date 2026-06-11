# Phân tích codebase & Lộ trình phát triển — Smart Home Security System

> Tài liệu phân tích hiện trạng, đề xuất cải tiến **tối ưu chi phí**, và kiến trúc tích hợp
> **Raspberry Pi 5** (nhận diện khuôn mặt mở khóa + cảnh báo cạy/phá cửa).
>
> *Ngày tạo: 2026-06-09 · Đối chiếu với: `wpf_dasboard_preparation.md` · Định hướng: tiết kiệm tối đa*
> *Độ tin cậy: Cao cho phần codebase (đọc trực tiếp); Trung bình–Cao cho giá phần cứng (có trích dẫn, giá thị trường biến động).*

---

## Tóm tắt điều hành (Executive Summary)

- **App WPF C# (.NET 8) đã hoàn thiện ~90% phần desktop**: Dashboard, CRUD Users/RFID/PIN, Access Logs,
  Device Control, Alerts, tích hợp MQTT realtime, biểu đồ LiveCharts, hash PIN bằng BCrypt. Kiến trúc
  MVVM sạch (CommunityToolkit.Mvvm) + DI đầy đủ.
- **Phần "vật lý" của hệ thống chưa tồn tại**: **chưa có firmware ESP32 nào**, và **chưa có code
  Raspberry Pi / nhận diện khuôn mặt nào** trong repo. Đây là phần còn lại lớn nhất của dự án.
- **Codebase đã sẵn "móc nối" để cắm Pi 5**: cột `access_logs.method`, `alerts.alert_type`,
  `esp_nodes.node_type` đều là chuỗi tự do → chỉ cần thêm giá trị mới (`FACE_RECOGNITION`,
  `FORCED_ENTRY`, `raspberry_pi`), gần như **không phải đổi schema**.
- **3 vấn đề cần vá sớm**: (1) **lệch quy ước MQTT topic** giữa tài liệu (`home/...`) và code thực tế
  (`smarthome/...`); (2) **mật khẩu DB hardcode** trong `appsettings.json`; (3) **MQTT không
  auth/không TLS**.
- **Định hướng chi phí**: dùng **Raspberry Pi 5 làm hub luôn-bật** (Mosquitto + DB + nhận diện khuôn
  mặt), **CPU-only không cần Hailo**, **Mosquitto self-host** thay cloud, chống giả mạo bằng blink
  detection $0. **Tổng phần cứng ~170–250 USD, điện ~8 USD/năm, phần mềm 0 USD.**

---

## Phần 1 — Đánh giá hiện trạng codebase

### 1.1. Công nghệ thực tế (đọc từ `Smart_Home.csproj`)

| Thành phần | Phiên bản | Ghi chú |
|---|---|---|
| Target framework | `net8.0-windows` | WPF, nullable + implicit usings bật |
| MVVM | CommunityToolkit.Mvvm `8.4.2` | `ObservableObject`, `[ObservableProperty]`, `RelayCommand` |
| Database | Npgsql.EntityFrameworkCore.PostgreSQL `8.0.2` | **PostgreSQL**, code-first, `IDbContextFactory` |
| MQTT | MQTTnet `4.3.7.1207` + ManagedClient | auto-reconnect, QoS AtLeastOnce |
| Biểu đồ | LiveChartsCore.SkiaSharpView.WPF `2.0.4` | nhiệt độ/độ ẩm realtime |
| Bảo mật | BCrypt.Net-Next `4.2.0` | hash PIN |
| DI/Config | Microsoft.Extensions.DependencyInjection / Configuration.Json | cấu hình trong `App.xaml.cs` |

### 1.2. Đối chiếu Plan (`wpf_dasboard_preparation.md`) vs Thực tế

| Hạng mục theo plan | Trạng thái | Bằng chứng trong code |
|---|---|---|
| GĐ1: DB + EF Core + AppDbContext | ✅ DONE | `Data/AppDbContext.cs` (12 `DbSet`), `Smart_home_security_database.sql` |
| GĐ2: Dashboard | ✅ DONE | `ViewModels/DashboardViewModel.cs` (~246 dòng) + `View/DashboardView.xaml` |
| GĐ3: CRUD User/RFID/PIN, khóa/gán thẻ | ✅ DONE | `UsersViewModel`, `RFIDCardsViewModel`, `PinCodesViewModel` |
| GĐ4: Access Logs + Alerts + đánh dấu xử lý | ✅ DONE | `AccessLogsViewModel`, `AlertsViewModel` (lọc theo ngày) |
| GĐ5: MQTT realtime | ✅ DONE (phía WPF) | `Service/MqttClientService.cs`, subscribe trong `DashboardViewModel` |
| GĐ6: Điều khiển thiết bị qua MQTT | ✅ DONE (phía WPF) | `DeviceControlViewModel` publish `smarthome/{device_code}/set` |
| Firmware ESP32 (cửa + nhà) | ❌ MISSING | **Không có** file `.ino/.cpp/.h`, không PlatformIO |
| Nhận diện khuôn mặt / Raspberry Pi | ❌ MISSING | **Không có** file `.py`, không OpenCV/dlib/MediaPipe |
| Cảnh báo cạy/phá cửa | ❌ MISSING | Chưa có sensor logic; `Alert` mới có các type môi trường |

> **Kết luận**: phần phần mềm desktop coi như xong. Hướng phát triển hiện tại **đúng đắn và sạch sẽ**,
> nhưng toàn bộ **lớp thiết bị (ESP32 + Pi 5)** vẫn nằm trên giấy — đây là nơi cần dồn lực tiếp theo.

### 1.3. Điểm mạnh kiến trúc (nên giữ)

- **MVVM chuẩn + DI**: ViewModel là transient, `IMqttService` singleton, `AddDbContextFactory` →
  dễ mở rộng, tránh rò rỉ `DbContext`.
- **Soft-delete** cho user/thẻ/PIN (giữ vết audit) — đúng yêu cầu nghiệp vụ an ninh.
- **Lưu `raw_payload` (JSONB)** ở `access_logs`/`sensor_readings`/`alerts` → audit tốt, dễ debug.
- **Hash PIN bằng BCrypt** — đúng (không lưu plaintext).
- **ManagedClient auto-reconnect** + hàng đợi lệnh khi mất kết nối — bền với mạng chập chờn.

### 1.4. Điểm cần cải thiện

> **Cập nhật 2026-06-10**: #1–#5 đã được xử lý trong code (chi tiết ở cột Trạng thái + ghi chú cuối mục).
> Chỉ còn #6 (cosmetic) và một vài việc **vận hành** (xoay vòng mật khẩu thật, bật auth/TLS trên broker).

| Mức độ | Vấn đề | Trạng thái | Chi tiết |
|---|---|---|---|
| 🔴 CRITICAL | **Secrets hardcode** | ✅ Đã xử lý | Mật khẩu đã gỡ khỏi `appsettings.json` (để rỗng) + có `appsettings.example.json`. `App.xaml.cs` đọc theo thứ tự **user-secrets → env (`SMART_HOME_CONNECTION_STRING` / `ConnectionStrings__DefaultConnection`, `MQTT_USERNAME` / `MQTT_PASSWORD`) → appsettings**; `UserSecretsId` đã bật trong csproj. *Còn lại (vận hành): xoay vòng mật khẩu thật.* |
| 🔴 CRITICAL | **MQTT không bảo mật** | ✅ Đã xử lý (phía WPF) | `MqttClientService` hỗ trợ TLS (`UseTls`, cổng 8883), xác thực user/pass, và **chỉ tin CA self-signed** qua `CustomRootTrust` (không tin toàn máy); `AllowUntrustedCertificates` chỉ cho DEV. *Còn lại (vận hành): bật auth/TLS trên Mosquitto.* |
| 🟠 HIGH | **Lệch quy ước topic** | ✅ Đã xử lý | `Service/MqttTopics.cs` là **nguồn chân lý duy nhất** cho topic (prefix `smarthome/`), dùng ở Dashboard/DeviceControl/NodePresence; alias `home/...` chỉ còn để tra cứu (Phụ lục A). |
| 🟡 MEDIUM | **Business logic trong ViewModel** | ✅ Đã xử lý | Đã tách **service layer** (`UserService`, `PinCodeService`, `RfidCardService`, `DeviceService`, `AccessLogService`, `AlertService`, `DashboardService`, `IPasswordHasher`) trả `OperationResult`; **7 ViewModel** giờ chỉ gọi service (gỡ EF Core/BCrypt trực tiếp), đăng ký đầy đủ trong DI. **Test: 47 unit test (xUnit + EF InMemory) — 95.4% dòng / 85.8% nhánh** trên lớp service unit-test (dự án `Smart_Home.Tests`). |
| 🟡 MEDIUM | **Chưa xử lý online/offline node** | ✅ Đã xử lý | `NodePresenceService` (singleton) theo dõi LWT (`smarthome/status/{node}/online`, retained) + birth message + timeout heartbeat; phát `DEVICE_OFFLINE` đúng **1 lần** khi online→offline và cập nhật `EspNode.Status/LastSeenAt`. |
| 🟢 LOW | **Đặt tên thư mục** | ⏳ Còn lại | `View/`, `Service/` (số ít) vẫn lệch `Views/`, `Services/`. **Chưa sửa** vì đổi tên thư mục trong WPF kéo theo cập nhật namespace/`x:Class`/`clr-namespace` — rủi ro cao, lợi ích chỉ cosmetic. |

> **Tóm tắt thay đổi (2026-06-10)** — hoàn tất **#4**: nối service vào DI trong `App.xaml.cs`, refactor toàn bộ **7 ViewModel** sang gọi service (xoá ~300 dòng EF Core/BCrypt trùng lặp; service trước đó là dead code), và thêm dự án **`Smart_Home.Tests`** (47 test xanh; 95.4% line / 85.8% branch trên lớp service unit-test — loại trừ `MqttClientService`/`NodePresenceService` vì cần integration test với broker thật / luồng STA). #1, #2, #3, #5 đã có cơ chế sẵn trong code; chỉ còn việc vận hành. #6 giữ nguyên (cosmetic).

---

## Phần 2 — Đề xuất cải tiến & Tối ưu chi phí

### 2.1. Kiến trúc đề xuất: Raspberry Pi 5 làm "hub luôn-bật"

Ý tưởng cốt lõi để **tiết kiệm điện và tăng độ sẵn sàng**: tách phần "luôn chạy" ra khỏi PC Windows.

```text
        ┌──────────────────────── Mạng nội bộ (LAN / VLAN IoT) ────────────────────────┐
        │                                                                              │
  ┌─────┴─────┐         MQTT/TLS        ┌────────────────────────────┐                 │
  │ ESP32 CỬA │ ───────────────────────►│   Raspberry Pi 5 (HUB)     │◄──── MQTT/TLS ──┐│
  │ RFID+Keypad│  smarthome/door/#      │  • Mosquitto (broker)      │                ││
  │ Solenoid   │◄───────────────────────│  • SQLite / PostgreSQL     │   ┌───────────┴┴─┐
  │ Reed/SW420 │  smarthome/door/control│  • MQTT→DB bridge (Python) │   │ ESP32 NHÀ    │
  │ MPU6050+Còi│                        │  • Nhận diện khuôn mặt (CV)│   │ DHT22/MQ-2   │
  └────────────┘                        └─────────────┬──────────────┘   │ Đèn/Quạt/LED │
                                                       │ TCP (chỉ khi cần)└──────────────┘
                                                ┌──────┴───────┐
                                                │ PC Windows   │  ← chỉ bật khi cần xem/quản trị
                                                │ App WPF (UI) │
                                                └──────────────┘
```

**Vì sao đáng làm**: Pi 5 idle ~2.7–3.6 W → ~**8 USD/năm** điện; PC để 24/7 tốn 50–100 W →
**130–275 USD/năm** ([raspberry.tips power 2026](https://raspberry.tips/en/raspberrypi-tutorials/raspberry-pi-power-consumption-update-2026-all-models-compared),
[PowerPlug idle PC](https://powerplug.ai/how-much-electricity-do-office-pcs-use-when-idle)).
→ **Tiết kiệm ~120–270 USD/năm** và hệ thống an ninh vẫn chạy kể cả khi tắt PC.

### 2.2. MQTT Broker — self-host Mosquitto (miễn phí)

Với mạng nội bộ 3–4 thiết bị, **Mosquitto trên Pi 5** là lựa chọn rẻ và bền nhất; cloud broker
(HiveMQ/EMQX/AWS IoT) là thừa và có thể phát sinh phí.

| Phương án | Free tier (2025–2026) | Phù hợp? |
|---|---|---|
| **Mosquitto self-host** | Miễn phí, không giới hạn, RAM <200 KB | ✅ **Khuyến nghị** |
| HiveMQ Cloud | 100 kết nối, giới hạn retention | ❌ thừa cho LAN |
| EMQX Serverless | 1M session-min/tháng | ❌ thừa cho LAN |
| AWS IoT Core | 500K msg/tháng (12 tháng đầu) | ❌ phức tạp, vendor lock-in |

Nguồn: [EMQX so sánh broker](https://www.emqx.com/en/blog/a-comprehensive-comparison-of-open-source-mqtt-brokers-in-2023),
[ThinkRobotics Mosquitto trên Pi](https://thinkrobotics.com/blogs/learn/complete-guide-to-mqtt-broker-setup-on-raspberry-pi-in-2025).

### 2.3. Database — cân nhắc SQLite, giữ PostgreSQL nếu đa-writer

- Nếu **chỉ WPF** ghi DB → **SQLite** đủ dùng, không cần tiến trình server, backup chỉ là copy 1 file.
- Nếu **nhiều thành phần cùng ghi** (WPF + bridge Pi + tương lai có API) → **giữ PostgreSQL** (chạy
  trên Pi 5) vì kiểm soát truy cập đồng thời tốt hơn.
- EF Core 8 hỗ trợ cả hai; đổi provider gần như 1 dòng:

```csharp
// App.xaml.cs — chỉ đổi 1 dòng khi muốn dùng SQLite:
services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite("Data Source=smarthome.db"));
// thay cho: o.UseNpgsql(connectionString);
```

> **Khuyến nghị (tiết kiệm tối đa)**: vì kiến trúc đề xuất có **bridge trên Pi 5 ghi DB song song với
> WPF**, nên **giữ PostgreSQL nhưng chuyển nó lên Pi 5** (cùng chỗ với Mosquitto). Nếu muốn đơn giản
> hóa giai đoạn đầu, dùng SQLite trên chính Pi và để WPF truy cập qua bridge.
> Nguồn: [EF Core providers](https://learn.microsoft.com/en-us/ef/core/providers/),
> [chuyển SQLite→PostgreSQL EF Core](https://didourebai.medium.com/switching-ef-core-from-sqlite-to-postgresql-a-complete-guide-for-net-developers-e4b3174243bf).

### 2.4. Chuẩn hóa MQTT topic (giữ prefix `smarthome/`)

Vì code WPF **đã dùng `smarthome/`**, ta giữ prefix này (ít sửa nhất) và viết firmware theo đó.

| Tài liệu cũ (`home/...`) | Chuẩn hóa (`smarthome/...`) | Hướng |
|---|---|---|
| `home/door/status` | `smarthome/status/door` | ESP32 cửa → publish |
| `home/door/control` | `smarthome/door/control` | WPF/Pi → ESP32 cửa |
| `home/door/rfid` | `smarthome/door/rfid` | ESP32 cửa → publish UID |
| `home/door/keypad` | `smarthome/door/keypad` | ESP32 cửa → publish PIN event |
| `home/sensor/status` | `smarthome/sensor/home` | ESP32 nhà → publish |
| `home/light/control` | `smarthome/{device_code}/set` | WPF → ESP32 nhà |
| *(mới)* | `smarthome/face/result` | Pi 5 → publish kết quả nhận diện |
| *(mới)* | `smarthome/door/breach` | ESP32 cửa → cảnh báo phá cửa |

Bảng đầy đủ ở **Phụ lục A**.

### 2.5. Tách Service Layer (chuẩn bị cho test 80%)

Dần rút logic EF Core ra khỏi ViewModel thành service (đúng nguyên tắc "nhiều file nhỏ"):

```csharp
public interface IAccessLogService {
    Task<AccessLog> RecordAsync(AccessLogDto dto);            // dùng chung cho RFID/PIN/FACE
    Task<IReadOnlyList<AccessLog>> QueryAsync(DateOnly from, DateOnly to, string? method);
}
// ViewModel chỉ gọi service → dễ mock, dễ viết unit test (AAA pattern).
```

### 2.6. Bảo mật chi phí $0

```ini
# Mosquitto: bật auth + TLS (chạy 1 lần trên Pi 5)
# /etc/mosquitto/mosquitto.conf
allow_anonymous false
password_file /etc/mosquitto/pwfile      # tạo bằng: mosquitto_passwd -c /etc/mosquitto/pwfile wpf
listener 8883
certfile /etc/mosquitto/server.crt        # self-signed, openssl, miễn phí
keyfile  /etc/mosquitto/server.key
cafile   /etc/mosquitto/ca.crt
```

```powershell
# WPF: bỏ secrets khỏi appsettings.json, đưa vào biến môi trường / user-secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=pi5;Database=Smart_Home_db;Username=app;Password=<đã-xoay-vòng>"
```

- **Xoay vòng** mật khẩu PostgreSQL đang lộ; cập nhật `MqttOptions.Username/Password` trong WPF.
- (Tùy chọn $30) tách **VLAN IoT** cho ESP32/Pi để cô lập khỏi LAN chính.

Nguồn: [Cedalo MQTT TLS](https://www.cedalo.com/blog/mqtt-tls-configuration-guide),
[Steve's Internet Guide — Mosquitto TLS](http://www.steves-internet-guide.com/mosquitto-tls/),
[XDA — VLAN cho smart home](https://www.xda-developers.com/network-segmentation-is-smart-home-security-step-nobody-talks-about/).

---

## Phần 3 — Tích hợp Raspberry Pi 5: Nhận diện khuôn mặt mở khóa cửa

### 3.1. Stack phần mềm khuyến nghị (CPU-only, tiết kiệm)

| Vai trò | Thư viện chọn | FPS Pi 5 (CPU) | Độ chính xác | License |
|---|---|---|---|---|
| Phát hiện khuôn mặt | **OpenCV DNN** (SSD/MobileNet) | ~10–15 | ~84.5% | BSD (thương mại OK) |
| Nhận diện (embedding) | **face_recognition / dlib** (CNN) | ~8 | 99.38% LFW | mở, free |
| Liveness chống giả mạo | **MediaPipe** (blink/EAR) | ~15–20 | ~95% chặn ảnh in | Apache-2.0 |

> **Không cần Hailo AI HAT+** cho 1 cửa: với độ trễ mở khóa ~200–300 ms thì CPU-only là đủ; Hailo
> thêm **70–110 USD** và bị nghẽn PCIe x1 trên Pi 5 (hiệu năng thực ~25% so với benchmark hãng).
> Chỉ cân nhắc Hailo khi xử lý **nhiều luồng camera** đồng thời.
> Nguồn: [MDPI — face recognition trên Pi/CNN](https://www.mdpi.com/2079-3197/10/9/148),
> [face_recognition (ageitgey)](https://github.com/ageitgey/face_recognition),
> [Pi AI HAT+ giá](https://www.raspberrypi.com/products/ai-hat/),
> [MediaPipe trên Pi 5](https://medium.com/@clarencechng/practical-computer-vision-using-mediapipe-on-raspberry-pi-5-43ad6277a825).

### 3.2. Chống giả mạo (anti-spoofing) chi phí thấp

- **Blink detection (EAR)** bằng MediaPipe FaceMesh: yêu cầu chớp mắt trong cửa sổ thời gian → ảnh in
  / màn hình điện thoại không qua được. **$0.**
- **Frame-differencing**: phát hiện cảnh tĩnh (ảnh) vs khuôn mặt thật có vi chuyển động → chặn replay.
- Nâng cấp khi cần: **challenge-response** ("quay trái/cười") hoặc thêm **yếu tố thứ hai RFID/PIN**
  (đã có sẵn trong hệ thống) → multi-factor mạnh mà gần như $0.
  Nguồn: [KBY-AI liveness free](https://kby-ai.com/top-7-free-on-premise-face-liveness-detection-solutions/),
  [MobiDev anti-spoofing](https://mobidev.biz/blog/face-anti-spoofing-prevent-fake-biometric-detection).

### 3.3. Tối ưu hiệu năng / điện năng

- Chỉ chạy nhận diện khi **PIR báo chuyển động** (PIR ~15µA) → giảm ~75% điện, camera ngủ khi vắng.
- Capture **480p**, downscale; **cache embedding** enrolled (10 mặt ~5 KB), so khớp <1 ms.
- Xử lý **mỗi 3 frame** thay vì mọi frame → giảm 2–3× CPU; độ trễ ~300 ms (chấp nhận được cho cửa).

### 3.4. Luồng tích hợp với hệ thống hiện có

```text
[Pi5] PIR kích hoạt → camera → phát hiện mặt → liveness (blink) → khớp embedding
      └─► publish smarthome/face/result {userId, name, confidence, live:true, ts}
[Pi5 bridge] ghi access_logs (method="FACE_RECOGNITION", result="success")
      └─► publish smarthome/door/control {command:"unlock", duration:5, source:"face"}
[ESP32 cửa] nhận → kích solenoid mở 5s → publish smarthome/status/door {door, lock}
[WPF] subscribe sẵn smarthome/status/# → Dashboard cập nhật realtime (không phải sửa nhiều)
```

**Code sketch Pi 5 (Python, rút gọn):**

```python
import cv2, face_recognition, paho.mqtt.client as mqtt, json, time
known = load_known_encodings()          # nạp embedding đã enroll (cache)

def on_motion():                        # gọi khi PIR HIGH
    cap = cv2.VideoCapture(0); cap.set(3,640); cap.set(4,480)
    for _ in range(45):                 # ~3s, xử lý mỗi 3 frame
        ok, frame = cap.read()
        if not ok: break
        if not blink_detected(frame):   # MediaPipe EAR — liveness
            continue
        locs = face_recognition.face_locations(frame, model="hog")
        encs = face_recognition.face_encodings(frame, locs)
        for e in encs:
            m = face_recognition.compare_faces(known.vecs, e, tolerance=0.45)
            if any(m):
                user = known.users[m.index(True)]
                publish("smarthome/face/result",
                        json.dumps({"userId":user.id,"name":user.name,
                                    "confidence":0.9,"live":True,"ts":time.time()}))
                publish("smarthome/door/control",
                        json.dumps({"command":"unlock","duration":5,"source":"face"}))
                cap.release(); return
    cap.release()
```

### 3.5. Bill of Materials — Pi 5 door node (~135 USD)

| Linh kiện | Giá (USD) | Ghi chú |
|---|---|---|
| Raspberry Pi 5 4GB | 75 | đủ cho 1 luồng nhận diện |
| Camera Module 3 (hoặc webcam USB rẻ) | 25 (hoặc ~10) | 12MP autofocus |
| Nguồn USB-C 27W | 10 | bên thứ ba |
| Active Cooler | 5 | cần khi chạy CV liên tục |
| microSD 64GB | 15 | NVMe **không cần** cho use case này |
| PIR motion | 5 | trigger tiết kiệm điện |
| **Tổng lõi** | **~135** | chưa gồm cơ cấu khóa (xem Phần 4/6) |

Nguồn giá: [Raspberry Pi 5](https://www.raspberrypi.com/products/raspberry-pi-5/),
[Camera Module 3](https://www.raspberrypi.com/products/camera-module-3/),
[Active Cooler](https://www.raspberrypi.com/products/active-cooler/),
[SD cards](https://www.raspberrypi.com/products/sd-cards/).

---

## Phần 4 — Cảnh báo cạy/phá cửa (Forced-Entry Detection)

### 4.1. Nguyên tắc: logic chạy trên ESP32, còi kêu cục bộ

Để báo động **vẫn hoạt động khi mất mạng / PC & Pi tắt**, đặt logic phát hiện + kích còi **ngay trên
ESP32 cửa** (độ trễ <100 ms). MQTT chỉ để ghi log + notification (thứ yếu).

### 4.2. Cảm biến rẻ + vai trò

| Cảm biến | Vai trò | Giá (USD) | Vì sao |
|---|---|---|---|
| Reed switch | Trạng thái cửa đóng/mở | ~1 | rẻ; **một mình không đủ** phát hiện cạy |
| **SW-420** rung | Phát hiện cạy/đá/đập | ~0.5 | nhạy rung, chỉnh ngưỡng bằng biến trở |
| **MPU6050** | Đo cường độ va đập + nghiêng cửa | ~5 | phân biệt gõ nhẹ vs đá mạnh (đo G) |
| Relay + còi 110dB | Báo động cục bộ | ~12 | kích trực tiếp, không cần mạng |

### 4.3. Sensor fusion logic (giảm báo giả)

```c
// ESP32 — pseudo, chạy cục bộ
bool forcedEntry =
   (lockState == LOCKED)                       // cửa đang khóa
   && (vibrationCount >= 2 within 300ms)        // SW-420 rung liên tục
   && (accelPeak > 1.5 /*G*/)                   // MPU6050 va đập mạnh
   && (reed == OPEN within 300ms);              // cửa bị bật ra dù chưa unlock

if (forcedEntry) {
    digitalWrite(SIREN_RELAY, HIGH);            // còi kêu NGAY (<100ms)
    mqttPublish("smarthome/door/breach",
        "{\"type\":\"FORCED_ENTRY\",\"accel\":x,\"ts\":...}", /*QoS*/1);
}
// Mở hợp lệ: lockState=UNLOCKED rồi reed=OPEN → KHÔNG báo động.
```

Phân biệt gõ cửa/gió vs phá cửa dựa trên **ngưỡng + debounce + cửa sổ thời gian + kết hợp nhiều tín
hiệu**. Nguồn: [Alarm Grid — shock sensors](https://www.alarmgrid.com/browse/shock-sensors),
[SunFounder MPU6050](https://docs.sunfounder.com/projects/ultimate-sensor-kit/en/latest/components_basic/05-component_mpu6050.html),
[IEEE — vibration threshold](https://ieeexplore.ieee.org/document/9250475).

### 4.4. Tích hợp với WPF/DB

- ESP32 publish `smarthome/door/breach` (QoS 1) → bridge/WPF tạo bản ghi `alerts` với
  `alert_type = "FORCED_ENTRY"`, `level = "critical"`, lưu `raw_payload`.
- WPF (đã subscribe `smarthome/status/#`/có thể thêm `smarthome/door/breach`) hiển thị cảnh báo đỏ +
  cho phép **tắt còi từ xa** qua `smarthome/door/control {command:"alarm_off"}` (đã có khung trong plan).
- (Tùy chọn) bridge gửi **push notification** điện thoại.

### 4.5. BOM cảm biến cửa (~33 USD)

| Linh kiện | Giá | | Linh kiện | Giá |
|---|---|---|---|---|
| ESP32 board | 6 | | Reed switch | 1 |
| SW-420 | 0.5 | | MPU6050 | 5 |
| Relay module | 2.5 | | Còi 110dB + 12V | 10 |
| Dây + hộp | 5 | | **Tổng** | **~33** |

Nguồn: [ESP32 alternatives/giá](https://www.espboards.dev/blog/esp32-alternatives/),
[SW-420](https://www.amazon.com/Hiletgo-SW-420-Vibration-Sensor-Arduino/dp/B00HJ6ACY2),
[relay module](https://www.amazon.com/DIYables-Arduino-ESP8266-Raspberry-Channel/dp/B0B1ZHXXXD).

---

## Phần 5 — Lộ trình triển khai (Roadmap)

> Mỗi giai đoạn có **việc cần làm · file/topic/bảng liên quan · tiêu chí hoàn thành**.

### GĐ A — Vá nhanh codebase (1–2 ngày, $0)
> **Trạng thái (2026-06-10)**: phần **code đã xong** (xem mục 1.4 #1–#3). Còn lại là việc **vận hành**.
- ✅ Chuẩn hóa topic về `smarthome/` — gom về `Service/MqttTopics.cs` (nguồn chân lý duy nhất).
- ✅ Đưa secrets ra env/user-secrets (`App.xaml.cs` đọc user-secrets → env → appsettings). *Còn lại: **xoay vòng** mật khẩu DB thật.*
- ✅ Client hỗ trợ auth + TLS (`MqttOptions.Username/Password/UseTls/CaCertPath`). *Còn lại: bật auth/TLS trên Mosquitto.*
- ✅ *Done khi*: WPF kết nối broker có auth/TLS, không còn secret trong file commit.

### GĐ B — Firmware 2 node ESP32 (3–5 ngày)
- Node **cửa**: RFID (MFRC522) + keypad 4x4 + solenoid (relay) + reed + SW-420 + MPU6050 + còi.
  Publish `smarthome/door/rfid|keypad|status|breach`; subscribe `smarthome/door/control`.
- Node **nhà**: DHT22 + MQ-2 + relay đèn/quạt/LED. Publish `smarthome/sensor/home`,
  `smarthome/status/...`; subscribe `smarthome/{device_code}/set`.
- Dùng **LWT (Last Will)** để Dashboard biết node offline → phát `DEVICE_OFFLINE`.
- ✅ *Done khi*: Dashboard WPF hiển thị sensor thật + điều khiển đèn/quạt thật qua MQTT.

### GĐ C — Pi 5 hub + nhận diện khuôn mặt (4–7 ngày)
- Cài Mosquitto (+ PostgreSQL hoặc SQLite) trên Pi 5; viết **bridge Python** subscribe MQTT → ghi DB.
- Module nhận diện: OpenCV DNN + face_recognition + MediaPipe (blink), trigger bằng PIR.
- Enroll khuôn mặt người dùng (map tới `users.id`); publish `smarthome/face/result` + `door/control`.
- Bảng/cột: thêm giá trị `method="FACE_RECOGNITION"` vào `access_logs`; `esp_nodes.node_type="raspberry_pi"`.
- ✅ *Done khi*: đứng trước cửa → nhận diện đúng → cửa mở → `access_logs` có bản ghi FACE.

### GĐ D — Cảnh báo cạy/phá cửa (2–3 ngày)
- Hoàn thiện sensor fusion trên ESP32 cửa + còi cục bộ; publish `smarthome/door/breach`.
- WPF/bridge tạo `alerts(alert_type="FORCED_ENTRY", level="critical")`; nút **tắt còi từ xa**.
- Hiệu chỉnh ngưỡng SW-420/MPU6050 để giảm báo giả (gió, gõ cửa).
- ✅ *Done khi*: thử cạy cửa → còi kêu <100 ms + Alert đỏ trên Dashboard; gõ cửa nhẹ không báo.

### GĐ E — Nâng cấp tùy chọn (khi rảnh)
- ✅ **Đã xong**: tách **service layer** + **47 unit test** (95.4% line / 85.8% branch lớp service) — xem mục 1.4 #4.
- ✅ **Đã xong**: dashboard online/offline node qua `NodePresenceService` (LWT + heartbeat) — mục 1.4 #5.
- Còn lại: liveness nâng cao (challenge-response), push notification điện thoại; (tùy chọn) integration test cho
  `MqttClientService`/`NodePresenceService`; đổi tên thư mục `View/`→`Views/`, `Service/`→`Services/` (#6, cosmetic).

---

## Phần 6 — Ước tính chi phí tổng

| Hạng mục | Chi phí (USD) | Ghi chú |
|---|---|---|
| Pi 5 door node (nhận diện) | ~135 | Phần 3.5 |
| Cảm biến cạy/phá cửa (gắn ESP32 cửa) | ~33 | Phần 4.5 |
| 2 node ESP32 (cửa + nhà, RFID/keypad/solenoid/DHT22/MQ-2/relay) | ~90–150 | tùy linh kiện |
| Mosquitto / .NET 8 / OpenCV / Python | 0 | mã nguồn mở |
| **Tổng phần cứng** | **~170–250** | một lần |
| **Điện vận hành (Pi 5 24/7)** | **~8 / năm** | thay vì 130–275/năm nếu để PC 24/7 |
| VLAN switch (tùy chọn) | ~30 | cô lập IoT |

**So sánh self-host vs cloud**: cloud MQTT có thể phát sinh phí và phụ thuộc Internet; self-host
Mosquitto = $0/tháng, độ trễ thấp, dữ liệu nằm trong nhà. → **Self-host thắng** cho quy mô gia đình.

---

## Phần 7 — Sơ đồ chân ESP32 & Cấu trúc firmware

> Cập nhật theo lựa chọn: **đèn/quạt 12V/24V DC** (dùng MOSFET hoặc relay), code **mô tả cấu trúc +
> pseudocode**. Board giả định: **ESP32 DevKit V1 (WROOM-32, 38 chân)** — số GPIO giống nhau giữa các
> board, chỉ khác cách bố trí hàng chân.

### 7.0. Quy tắc dùng GPIO (đọc trước khi đấu dây)

| Nhóm chân | GPIO | Quy tắc |
|---|---|---|
| Flash nội (CẤM dùng) | 6–11 | Nối SPI flash, dùng sẽ treo/hỏng |
| UART debug (tránh) | 1 (TX0), 3 (RX0) | Giữ để nạp code/log |
| **Chỉ INPUT** (không output, không pull nội) | 34, 35, 36/VP, 39/VN | Chỉ đọc cảm biến; cần điện trở kéo **ngoài** |
| **Strapping** (cẩn thận lúc boot) | 0, 2, 5, 12, 15 | Dùng được nhưng phải đúng mức lúc khởi động |
| Output an toàn | 4, 13, 14, 16, 17, 18, 19, 21, 22, 23, 25, 26, 27, 32, 33 | Lý tưởng cho relay/MOSFET |
| **ADC1** (analog dùng được khi bật WiFi) | 32, 33, 34, 35, 36, 39 | **MQ-2 phải dùng nhóm này** |
| ADC2 (KHÔNG đọc analog khi WiFi bật) | 0,2,4,12,13,14,15,25,26,27 | Để dùng làm digital thôi |

> Nguồn: [Random Nerd — ESP32 GPIO reference](https://randomnerdtutorials.com/esp32-pinout-reference-gpios/),
> [Random Nerd — ESP32 ADC](https://randomnerdtutorials.com/esp32-adc-analog-read-arduino-ide/),
> [ADC2 xung đột WiFi — ESP32 Forum](https://www.esp32.com/viewtopic.php?t=7644).

### 7.1. Nguyên tắc điều khiển tải > 3,3V (qua relay / MOSFET)

ESP32 chỉ xuất 3,3V và dòng nhỏ → **mọi tải 12V (khóa solenoid, đèn/quạt DC, còi) phải đi qua khâu
đóng-cắt riêng**, KHÔNG nối thẳng vào chân ESP32. Vì tải là **12V DC**, có 2 lựa chọn:

**(A) MOSFET kênh-N logic-level** (IRLZ44N/IRLZ34N) — *khuyến nghị cho DC: rẻ, êm, hỗ trợ PWM dimmer*:

```text
ESP32 GPIO ──[220Ω]──┬── G (Gate)        ┌─ (+12V) ──► (+) Tải
                   [10kΩ]                 │
                     │            D (Drain)┘   (nối cực (−) của tải)
                    GND          S (Source) ── GND CHUNG (ESP32 GND + 12V GND)
Diode 1N4007 song song tải cảm (quạt DC, solenoid), catốt hướng về +12V (chống xung ngược).
```

**(B) Module relay opto-cách-ly** — dùng được cho mọi loại tải (kể cả AC sau này):

```text
ESP32 GPIO ── IN        (chọn module active-HIGH để IN=LOW lúc boot = TẮT)
ESP32 3V3  ── VCC       (nuôi phần logic/opto)
5V ngoài   ── JD-VCC    (nuôi cuộn relay — THÁO jumper VCC–JD-VCC để cách ly)
GND chung  ── GND
Tải 12V:  +12V ── COM ;  NO ── (+) tải ;  (−) tải ── GND(12V)
```

> Tải cảm (solenoid, quạt) **bắt buộc** có diode flyback 1N4007 (≥2× điện áp tải). **Phải chung GND**
> giữa ESP32 và nguồn 12V. Nguồn:
> [Random Nerd — ESP32 relay](https://randomnerdtutorials.com/esp32-relay-module-ac-web-server/),
> [Flyback diode — Zbotic](https://zbotic.in/flyback-diode-protecting-relay-and-motor-driver-circuits/).

### 7.2. Sơ đồ chân — NODE CỬA (`esp32-door`)

| Linh kiện | Tín hiệu | GPIO | Loại | Cấp nguồn | Ghi chú |
|---|---|---|---|---|---|
| MFRC522 (RFID, SPI) | SCK | 18 | out | **3V3** | VSPI; **không cấp 5V** |
| | MISO | 19 | in | | |
| | MOSI | 23 | out | | |
| | SS/SDA | 5 | out | | strapping nhưng CS nghỉ ở mức HIGH → an toàn |
| | RST | 17 | out | | |
| MPU6050 (I2C) | SDA | 21 | i/o | **3V3** | địa chỉ 0x68 |
| | SCL | 22 | out | | |
| Keypad 4x4 | Hàng R1–R4 | 13, 14, 25, 26 | out | 3V3 | quét hàng |
| | Cột C1–C4 | 27, 32, 33, 4 | in (PULLUP) | | đọc cột (pull-up nội) |
| Reed (cảm biến cửa) | DO | 35 | **in-only** | 3V3 | **cần điện trở kéo 10k lên 3V3 (ngoài)** |
| SW-420 (rung) | DO | 34 | **in-only** | 3V3/5V | module tự đẩy mức logic |
| 🔌 **Relay/MOSFET khóa solenoid 12V** | IN/Gate | 16 | out | điều khiển 3V3 | **TẢI 12V qua relay/MOSFET** + diode |
| 🔌 **Relay/MOSFET còi 12V** | IN/Gate | 2 | out | điều khiển 3V3 | strapping → chọn active-HIGH (tắt lúc boot); GPIO2 kèm LED onboard |

**Cấp nguồn node cửa**: `3V3` → MFRC522 + MPU6050 · `5V` → module relay (JD-VCC) + SW-420 ·
`12V` → solenoid + còi (đóng/cắt qua relay/MOSFET) · **GND chung tất cả**.

### 7.3. Sơ đồ chân — NODE NHÀ (`esp32-home`)

| Linh kiện | Tín hiệu | GPIO | Loại | Cấp nguồn | Ghi chú |
|---|---|---|---|---|---|
| DHT22 (nhiệt/ẩm) | DATA | 4 | i/o | **3V3** | + điện trở kéo 10k lên 3V3 |
| MQ-2 (gas) | AOUT | 34 | **in-only (ADC1)** | 5V (heater) | **AOUT qua cầu phân áp về ≤3,3V** rồi vào ADC |
| MQ-2 (tùy chọn) | DO | 35 | in-only | | ngưỡng số (báo gas nhanh) |
| 🔌 **Đèn phòng khách 12V** | Gate/IN | 25 | out | đk 3V3 | **qua MOSFET/relay** |
| 🔌 **Đèn ngủ 1 (12V)** | Gate/IN | 26 | out | | qua MOSFET/relay |
| 🔌 **Đèn ngủ 2 (12V)** | Gate/IN | 27 | out | | qua MOSFET/relay |
| 🔌 **Quạt 12V** | Gate/IN | 14 | out | | qua MOSFET/relay + **diode flyback** |
| 🔌 **LED ngoài cửa 12V (dimmable)** | Gate (PWM) | 13 | out | | **MOSFET + PWM (LEDC)** để chỉnh sáng/auto |

**Cấp nguồn node nhà**: `3V3` → DHT22 · `5V` → MQ-2 (heater; AOUT qua phân áp) ·
`12V` → đèn/quạt/LED (qua MOSFET/relay) · **GND chung**.

> MQ-2 nuôi 5V, AOUT có thể tới ~5V → **bắt buộc cầu phân áp** (vd 4k7 + 1k2, hoặc dùng module đã tích
> hợp) trước khi vào ADC 3,3V của ESP32. Nguồn:
> [MQ-2 với board 3V3 — One Transistor](https://www.onetransistor.eu/2023/01/mq-sensor-modules-3v3-boards-adc.html),
> [Random Nerd — DHT22](https://randomnerdtutorials.com/esp32-dht11-dht22-temperature-humidity-sensor-arduino-ide/).

### 7.4. Tổng hợp: linh kiện nào cần relay/MOSFET?

| Linh kiện | Điện áp | Đấu nối |
|---|---|---|
| MFRC522, MPU6050, DHT22, Keypad, Reed, SW-420, MQ-2 | 3,3V (hoặc tín hiệu ≤3,3V) | **Nối trực tiếp** chân ESP32 |
| Khóa solenoid, Còi | 12V | **Qua relay/MOSFET** (+ diode flyback) |
| Đèn phòng (×3), Quạt | 12V DC | **Qua MOSFET hoặc relay** (quạt + diode) |
| LED ngoài cửa | 12V DC | **Qua MOSFET + PWM** (dimmer/auto) |

### 7.5. Cấu trúc firmware (tách file theo từng linh kiện)

Arduino IDE biên dịch **mọi file `.ino/.cpp`** trong thư mục sketch và nạp `.h` qua `#include` →
có thể tách module gọn gàng. (Hoặc dùng **PlatformIO** với `src/` + `lib/`.)

```text
Firmware/
├── DoorNode/                  # NODE CỬA
│   ├── DoorNode.ino           # setup() + loop(): điều phối tổng
│   ├── config.h               # định nghĩa chân GPIO + WiFi/MQTT + tên topic
│   ├── WifiMqtt.h / .cpp      # WiFi + MQTT (PubSubClient/TLS), publish/subscribe, callback, LWT
│   ├── RfidReader.h / .cpp    # MFRC522: đọc UID thẻ
│   ├── Keypad4x4.h / .cpp     # quét keypad, gom mã PIN, '*'/'#'
│   ├── DoorLock.h / .cpp      # relay solenoid: unlock(duration)/lock()
│   └── Security.h / .cpp      # reed + SW-420 + MPU6050 (sensor fusion) + relay còi
└── HomeNode/                  # NODE NHÀ
    ├── HomeNode.ino
    ├── config.h
    ├── WifiMqtt.h / .cpp
    ├── EnvSensors.h / .cpp     # DHT22 + MQ-2 (đọc + cảnh báo gas)
    ├── Actuators.h / .cpp      # MOSFET/relay đèn + quạt (on/off theo lệnh MQTT)
    └── OutdoorLed.h / .cpp     # LED ngoài cửa: PWM + chế độ auto (theo ánh sáng)
```

**Trách nhiệm từng file** (nguyên tắc "một linh kiện = một module"):
- `config.h` — *toàn bộ* hằng số chân + WiFi/MQTT + topic, để đổi dây/đổi broker không phải sửa logic.
- `WifiMqtt` — kết nối lại tự động, đăng ký **LWT** (`smarthome/status/door` `{"online":false}`) để
  Dashboard biết node offline, parse JSON (ArduinoJson) cho lệnh đến.
- Mỗi module phần cứng phơi ra hàm `xxxInit()` (gọi trong `setup()`) + hàm nghiệp vụ (gọi trong `loop()`).

### 7.6. Pseudocode / skeleton các file chính

**`config.h` (node cửa):**
```cpp
// WiFi / MQTT
#define WIFI_SSID "..."   // điền
#define WIFI_PASS "..."
#define MQTT_HOST "192.168.1.10"   // IP Raspberry Pi 5 chạy Mosquitto
#define MQTT_PORT 8883             // TLS
#define MQTT_USER "door"
#define MQTT_PASS "..."
#define NODE_ID   "esp32-door"
// Topic (KHỚP prefix smarthome/ của WPF — xem Phụ lục A)
#define T_STATUS  "smarthome/status/door"
#define T_RFID    "smarthome/door/rfid"
#define T_KEYPAD  "smarthome/door/keypad"
#define T_BREACH  "smarthome/door/breach"
#define T_CONTROL "smarthome/door/control"   // subscribe
// Chân
#define RC522_SCK 18
#define RC522_MISO 19
#define RC522_MOSI 23
#define RC522_SS   5
#define RC522_RST 17
#define I2C_SDA 21
#define I2C_SCL 22
const byte KP_ROWS[4] = {13,14,25,26};
const byte KP_COLS[4] = {27,32,33,4};
#define PIN_RELAY_LOCK  16   // khóa solenoid 12V (active-HIGH)
#define PIN_RELAY_SIREN  2   // còi 12V (active-HIGH)
#define PIN_REED        35   // input-only + pull-up NGOÀI
#define PIN_SW420       34   // input-only
```

**`DoorNode.ino` (điều phối):**
```cpp
void setup(){
  Serial.begin(115200);
  pinMode(PIN_RELAY_LOCK, OUTPUT);  digitalWrite(PIN_RELAY_LOCK, LOW);  // mặc định KHÓA/OFF
  pinMode(PIN_RELAY_SIREN, OUTPUT); digitalWrite(PIN_RELAY_SIREN, LOW); // mặc định TẮT
  rfidInit(); keypadInit(); lockInit(); securityInit();
  wifiMqttInit();   // kèm LWT online=false
}
void loop(){
  mqttLoop();                                   // giữ kết nối + nhận lệnh
  String uid = rfidReadUid();
  if (uid.length()) mqttPublishJson(T_RFID, "{\"uid\":\""+uid+"\"}");
  String pin = keypadCollect();                 // trả mã khi nhấn '#'
  if (pin.length()) mqttPublishJson(T_KEYPAD, "{\"len\":"+String(pin.length())+"}");
  if (securityDetectForcedEntry()){             // fusion reed+SW420+MPU6050
    sirenOn();                                  // CÒI KÊU CỤC BỘ <100ms
    mqttPublishJson(T_BREACH, "{\"type\":\"FORCED_ENTRY\"}", /*QoS*/1);
  }
}
// callback MQTT (T_CONTROL):
//   {"command":"unlock","duration":5} -> doorUnlock(5);
//   {"command":"lock"}                -> doorLock();
//   {"command":"alarm_off"}           -> sirenOff();
```

**`Security.cpp` (sensor fusion — chống cạy/phá cửa):**
```cpp
bool securityDetectForcedEntry(){
  bool locked = doorIsLocked();                 // từ DoorLock
  bool vib    = vibrationBurst(PIN_SW420, 2, 300);   // >=2 lần rung trong 300ms
  float g     = mpuPeakG();                     // đỉnh gia tốc từ MPU6050
  bool opened = digitalRead(PIN_REED) == HIGH;  // cửa bị bật ra
  return locked && vib && g > 1.5 && opened;    // cửa đang khóa mà bị mở bằng lực
}
```

**`EnvSensors.cpp` (node nhà):**
```cpp
void readEnvAndPublish(){
  float t = dht.readTemperature();
  float h = dht.readHumidity();
  int  gas = analogRead(PIN_MQ2);               // ADC1 (GPIO34), đã qua phân áp
  mqttPublishJson("smarthome/sensor/home",
     "{\"temperature\":"+String(t,1)+",\"humidity\":"+String(h,0)+",\"gas\":"+String(gas)+"}");
  if (gas > GAS_THRESHOLD)
     mqttPublishJson("smarthome/alarm/gas", "{\"type\":\"GAS_HIGH\",\"value\":"+String(gas)+"}", 1);
}
```

**`Actuators.cpp` + `OutdoorLed.cpp` (node nhà):**
```cpp
void setLight(uint8_t pin, bool on){ digitalWrite(pin, on ? HIGH : LOW); }  // gate MOSFET
// LED ngoài cửa: PWM bằng LEDC
void outdoorInit(){ ledcSetup(0, 5000, 8); ledcAttachPin(PIN_LED_OUT, 0); }
void setOutdoor(uint8_t pct){ ledcWrite(0, map(pct,0,100,0,255)); }         // 0–100% độ sáng
// callback: {"device":"home.light.living_room","command":"turn_on"} -> setLight(PIN_LR, true)
```

### 7.7. Thư viện Arduino cần cài

| Thư viện | Dùng cho |
|---|---|
| `WiFi.h` + `WiFiClientSecure` | WiFi + TLS (built-in ESP32 core) |
| `PubSubClient` (hoặc `arduino-mqtt`) | MQTT client |
| `ArduinoJson` | parse/tạo payload JSON |
| `MFRC522` | đầu đọc RFID |
| `Keypad` (Mark Stanley) | keypad 4x4 |
| `DHT sensor library` + `Adafruit Unified Sensor` | DHT22 |
| `Adafruit_MPU6050` | gia tốc/gyro |

### 7.8. Lưu ý boot & an toàn (đừng bỏ qua)

- **Khởi tạo OUTPUT về trạng thái TẮT/KHÓA ngay đầu `setup()`** để tránh relay bật nhầm lúc khởi động.
- **Chân strapping** đang dùng: GPIO5 (SS — an toàn vì CS nghỉ HIGH) và GPIO2 (còi — chọn relay/MOSFET
  *active-HIGH* để mức LOW lúc boot = tắt). Tránh GPIO12/15/0 cho relay.
- **Chung GND** ESP32 ↔ nguồn 5V ↔ nguồn 12V; không bao giờ đưa 12V/5V vào chân GPIO.
- **Diode flyback** cho solenoid & quạt DC; **cầu phân áp** cho AOUT của MQ-2.
- **MQ-2 cần làm nóng** (vài phút, lần đầu 24–48h) trước khi giá trị ổn định.
- **MQTT QoS 1** cho lệnh quan trọng (unlock) và cảnh báo (breach/gas); còi vẫn kêu **cục bộ** kể cả
  khi mất mạng.

> **Nguồn phần cứng/firmware** (đã kiểm chứng):
> [ESP32 GPIO reference](https://randomnerdtutorials.com/esp32-pinout-reference-gpios/) ·
> [ESP32 ADC + WiFi](https://randomnerdtutorials.com/esp32-adc-analog-read-arduino-ide/) ·
> [RC522 trên ESP32](https://www.espboards.dev/sensors/rc522/) ·
> [Keypad trên ESP32](https://esp32io.com/tutorials/esp32-keypad) ·
> [DHT22 trên ESP32](https://randomnerdtutorials.com/esp32-dht11-dht22-temperature-humidity-sensor-arduino-ide/) ·
> [MQ-2 vào ADC 3V3](https://www.onetransistor.eu/2023/01/mq-sensor-modules-3v3-boards-adc.html) ·
> [Relay trên ESP32](https://randomnerdtutorials.com/esp32-relay-module-ac-web-server/) ·
> [MPU6050 (Adafruit)](https://www.arduino.cc/reference/en/libraries/adafruit-mpu6050/) ·
> [Flyback diode](https://zbotic.in/flyback-diode-protecting-relay-and-motor-driver-circuits/)
>
> *Lưu ý: relay là active-HIGH hay active-LOW, và module MQ-2 có sẵn cầu phân áp hay không — tùy nhà
> sản xuất, hãy kiểm tra datasheet module thực tế trước khi đấu.*

---

## Nguồn tham khảo (Sources)

**Nhận diện khuôn mặt / Pi 5**
- [MDPI — Face recognition CNN + Raspberry Pi](https://www.mdpi.com/2079-3197/10/9/148)
- [face_recognition (ageitgey) — GitHub](https://github.com/ageitgey/face_recognition)
- [InsightFace — GitHub](https://github.com/deepinsight/insightface)
- [MediaPipe trên Pi 5 — Medium](https://medium.com/@clarencechng/practical-computer-vision-using-mediapipe-on-raspberry-pi-5-43ad6277a825)
- [MDPI — Face Recognition Door Lock](https://www.mdpi.com/2624-831X/6/2/31)

**Phần cứng / giá**
- [Raspberry Pi 5](https://www.raspberrypi.com/products/raspberry-pi-5/) ·
  [Camera Module 3](https://www.raspberrypi.com/products/camera-module-3/) ·
  [Active Cooler](https://www.raspberrypi.com/products/active-cooler/) ·
  [AI HAT+](https://www.raspberrypi.com/products/ai-hat/) ·
  [SD cards](https://www.raspberrypi.com/products/sd-cards/)
- [raspberry.tips — power 2026](https://raspberry.tips/en/raspberrypi-tutorials/raspberry-pi-power-consumption-update-2026-all-models-compared)

**MQTT / DB / bảo mật**
- [EMQX — so sánh broker mở](https://www.emqx.com/en/blog/a-comprehensive-comparison-of-open-source-mqtt-brokers-in-2023) ·
  [ThinkRobotics — Mosquitto trên Pi](https://thinkrobotics.com/blogs/learn/complete-guide-to-mqtt-broker-setup-on-raspberry-pi-in-2025)
- [EF Core providers](https://learn.microsoft.com/en-us/ef/core/providers/) ·
  [Chuyển SQLite→PostgreSQL EF Core](https://didourebai.medium.com/switching-ef-core-from-sqlite-to-postgresql-a-complete-guide-for-net-developers-e4b3174243bf)
- [Cedalo — MQTT TLS](https://www.cedalo.com/blog/mqtt-tls-configuration-guide) ·
  [Steve's Internet Guide — Mosquitto TLS](http://www.steves-internet-guide.com/mosquitto-tls/) ·
  [XDA — VLAN IoT](https://www.xda-developers.com/network-segmentation-is-smart-home-security-step-nobody-talks-about/)

**Cảm biến cạy/phá cửa**
- [Alarm Grid — shock sensors](https://www.alarmgrid.com/browse/shock-sensors) ·
  [SunFounder — MPU6050](https://docs.sunfounder.com/projects/ultimate-sensor-kit/en/latest/components_basic/05-component_mpu6050.html) ·
  [SunFounder — SW-420](https://docs.sunfounder.com/projects/ultimate-sensor-kit/en/latest/components_basic/04-component_vibration.html) ·
  [IEEE — vibration threshold](https://ieeexplore.ieee.org/document/9250475)
- [XDA — ESP32 vs Raspberry Pi (power)](https://www.xda-developers.com/tasks-esp32-can-handle-better-than-raspberry-pi/)

> **Lưu ý độ tin cậy**: Giá linh kiện thay đổi theo nhà bán/thời điểm (Amazon thường cao hơn AliExpress
> ~30%); giá nguồn 27W chính hãng không niêm yết rõ — dùng ~$8–12 cho nguồn bên thứ ba. FPS Pi 5 cho
> MediaPipe/Hailo phần lớn là **ngoại suy từ Pi 4 + benchmark hãng**, nên kiểm chứng lại khi mua.

---

## Phụ lục A — Bảng MQTT topic chuẩn hóa đầy đủ

| Topic | Hướng | Payload (ví dụ) |
|---|---|---|
| `smarthome/status/door` | ESP32 cửa → | `{"door":"closed","lock":"locked"}` |
| `smarthome/door/rfid` | ESP32 cửa → | `{"uid":"A1B2C3","result":"success"}` |
| `smarthome/door/keypad` | ESP32 cửa → | `{"result":"failed","attempts":3}` |
| `smarthome/door/breach` | ESP32 cửa → | `{"type":"FORCED_ENTRY","accel":2.1}` |
| `smarthome/door/control` | → ESP32 cửa | `{"command":"unlock","duration":5,"source":"face"}` |
| `smarthome/sensor/home` | ESP32 nhà → | `{"temperature":30.5,"humidity":72,"gas":380,"light":1250}` |
| `smarthome/{device_code}/set` | → ESP32 nhà | `{"command":"turn_on"}` |
| `smarthome/face/result` | Pi 5 → | `{"userId":1,"name":"...","confidence":0.9,"live":true}` |

## Phụ lục B — Giá trị mới cần thêm (không đổi schema)

- `access_logs.method`: thêm `FACE_RECOGNITION` (bên cạnh `RFID`, `PIN`, `WPF_REMOTE`, `MANUAL_BUTTON`).
- `alerts.alert_type`: thêm `FORCED_ENTRY`, `BREAK_IN`, `UNAUTHORIZED_FACE`, `FACE_TIMEOUT`
  (bên cạnh `GAS_HIGH`, `DOOR_OPEN_TOO_LONG`, `DEVICE_OFFLINE`...).
- `esp_nodes.node_type`: thêm `raspberry_pi` (bên cạnh `esp32`).
- (Tùy chọn) `devices`: thêm bản ghi `rpi5.camera.face`, `rpi5.sensor.breach` để quản lý trên UI.
