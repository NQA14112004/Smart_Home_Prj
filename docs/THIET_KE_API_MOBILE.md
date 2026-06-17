# Thiết kế: ASP.NET Core Web API + Mobile app (MAUI) — Smart Home

> Mở rộng kiến trúc hiện tại theo mô hình **"3 nhánh chung 1 lõi"**: giữ nguyên app **WPF**, mở thêm
> nhánh **ASP.NET Core Web API** làm cổng, và nhánh **Mobile app tối giản** (.NET MAUI) chỉ 4 chức năng
> cốt lõi. KHÔNG clone lại toàn bộ chức năng quản trị của WPF.
>
> *Ngày tạo: 2026-06-17 · Bối cảnh: `PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md` (GĐ A–E) · Track mới: GĐ F*

---

## 0. Quyết định đã chốt

| Vấn đề | Lựa chọn | Lý do |
|---|---|---|
| Công nghệ Mobile | **.NET MAUI** | Cùng C#, tái dùng DTO (`Smart_Home.Contracts`), một skillset với team .NET, ra cả iOS + Android |
| Truy cập từ xa | **VPN Tailscale/WireGuard** | Điện thoại vào được Pi từ mọi nơi, KHÔNG phơi API ra Internet công khai, ~$0 |
| Camera | **Camera Pi 5 sẵn có, MJPEG proxy qua API** | Tái dùng phần cứng, $0 thêm, đủ xem cửa |
| Tái dùng code | **Tách thư viện Core + Contracts dùng chung** | DRY, không nhân đôi EF model; logic WPF giữ nguyên |

## 1. Nguyên tắc cốt lõi

Hiện tại WPF là **fat client 2 tầng**: nói **trực tiếp** với PostgreSQL (EF Core) **và** MQTT broker
trong LAN. Mobile **không được** đi theo đường đó — vừa mất an toàn (lộ DB/broker), vừa thường ở ngoài
mạng nhà. Vì vậy ta chèn **Web API làm cổng duy nhất** cho mobile.

```text
                 ┌──────────────── LÕI CHUNG (trên Pi 5 hub) ────────────────┐
                 │   PostgreSQL  +  Mosquitto broker  +  Pi5 bridge (writer) │
                 └───┬───────────────────┬───────────────────────┬──────────┘
        EF Core+MQTT │              REST/WS + MQTT                │ (ESP32 nodes / camera)
        (LAN, GIỮ NGUYÊN)                │
        ┌────────────┴───┐    ┌──────────┴───────────┐
        │  NHÁNH 1: WPF  │    │ NHÁNH 2: ASP.NET Core │  ← cổng cho mobile
        │  (admin, giữ   │    │  Web API (gateway)    │     REST + JWT + push + camera proxy
        │   nguyên)      │    └──────────┬───────────┘
        └────────────────┘               │ HTTPS + JWT (qua tailnet)
                                ┌─────────┴──────────┐
                                │ NHÁNH 3: Mobile app │  ← MAUI, tối giản, 4 chức năng
                                └─────────────────────┘
```

- **WPF** giữ nguyên 100% đường đi cũ (DB + MQTT trực tiếp trong LAN).
- **API** dùng lại `Smart_Home.Core` (EF + service + MQTT client), chạy **trên Pi 5** cạnh broker/DB.
- **Mobile** chỉ chạm API qua **HTTPS + JWT**, đi trong **tailnet** (Tailscale).
- **Pi5 bridge** vẫn là **writer luôn-bật** cho `sensor_readings`/`access_logs`/`alerts`; API chủ yếu
  **đọc** + ghi `device_commands` (mở khóa) + `mobile_push_tokens` → không xung đột.

## 2. Cấu trúc solution (sau GĐ F1)

```text
Smart_Home.Core/        (net8.0)         ✅ Models + AppDbContext + service thuần + MQTT client/topics
                                            (UserService, AccessLogService, AlertService, DashboardService,
                                             DeviceService, Pin/RfidService, PasswordHasher, OperationResult,
                                             IMqttService, MqttClientService, MqttTopics)
Smart_Home.Contracts/   (net8.0)         ✅ DTO thuần (KHÔNG EF/Npgsql) — API + Mobile dùng chung
Smart_Home/             (net8.0-windows) ✅ WPF: ViewModels/Views + NodePresenceService (UI), ref Core
Smart_Home.Tests/       (net8.0)         ✅ ref Core (47 test vẫn xanh)
Smart_Home.Api/         (net8.0)         🔜 GĐ F2: ASP.NET Core Web API, ref Core + Contracts
Smart_Home.Mobile/      (net8.0-android/ios) 🔜 GĐ F5: MAUI, ref Contracts (không kéo EF/Npgsql)
```

> **Namespace giữ nguyên** `Smart_Home.Models` / `.Data` / `.Service` khi dời sang Core → code WPF & XAML
> không phải sửa. `NodePresenceService` ở lại WPF vì dùng `DispatcherTimer` + `App.Current.Dispatcher`.

## 3. API surface — chỉ đủ 4 chức năng + auth

| Endpoint | Chức năng | Ghi chú |
|---|---|---|
| `POST /api/auth/login` | Đăng nhập | Dùng lại `users.password_hash` (BCrypt). Trả `AuthResponse` (JWT + refresh + role) |
| `POST /api/auth/refresh` | Làm mới token | Refresh token xoay vòng |
| `GET /api/access-logs?from&to&method&page&pageSize` | **Xem log ra/vào** | Tái dùng pattern `AccessLogService.QueryAsync` → `PagedResult<AccessLogDto>` |
| `POST /api/door/unlock` | **Mở khóa từ xa** | Role-gate → ghi `device_commands` + publish `smarthome/door/control {command:"unlock",source:"mobile"}` → `UnlockResponse(Accepted)` |
| `GET /api/door/events?page` | Danh sách "có người ở cửa" | Lịch sử sự kiện cửa (`DoorEventDto`) cho tab thông báo |
| `GET /api/camera/snapshot` | **Camera** (ảnh JPEG) | Proxy 1 frame từ camera Pi — tiết kiệm pin, poll định kỳ |
| `GET /api/camera/stream` | **Camera** (MJPEG live) | `multipart/x-mixed-replace` khi cần xem trực tiếp |
| `POST /api/devices/push-token` | Đăng ký nhận push | Lưu `mobile_push_tokens` (`PushTokenRequest`) |
| *HostedService nền* | **Thông báo có người ở cửa** | Sub MQTT door topics → đẩy FCM/APNs + SignalR (khi app mở) |

DTO tương ứng đã có trong `Smart_Home.Contracts/Dtos/` (GĐ F1).

## 4. Ánh xạ 4 chức năng → dữ liệu & luồng

| Chức năng | Nguồn dữ liệu | Luồng |
|---|---|---|
| Xem log ra/vào | `access_logs` (method RFID/PIN/FACE_RECOGNITION/REMOTE_UNLOCK) | Mobile `GET /api/access-logs` → API đọc DB → `PagedResult<AccessLogDto>` |
| Mở khóa từ xa | MQTT `smarthome/door/control` (đã có) | Mobile `POST /api/door/unlock` → API ghi `device_commands` + publish MQTT → ESP32 cửa mở; bridge ghi `access_logs` |
| Thông báo có người ở cửa | MQTT `smarthome/face/result`, `door/rfid`, `door/keypad`, `door/breach`; bảng `alerts` | API HostedService sub các topic → đẩy push (FCM/APNs) + SignalR; tab in-app đọc `GET /api/door/events` |
| Theo dõi camera | Camera Pi 5 (MJPEG) | API proxy `snapshot`/`stream` (JWT) ← nguồn MJPEG cục bộ trên Pi |

## 5. Bảo mật

- **Tailscale** trên Pi + điện thoại → API **bind tailnet (100.x) + localhost**, KHÔNG phơi public. Traffic
  đã mã hóa WireGuard; thêm HTTPS qua `tailscale serve`.
- **JWT** ký bằng khóa trong `.env` (đồng bộ kỷ luật `.env` sẵn có); access-token ngắn hạn + refresh xoay vòng.
- **Role-based**: mở khóa cần role cao (vd `admin`/`owner`); xem log/camera/thông báo theo role.
- **Rate-limit** endpoint `unlock` (ASP.NET Core rate limiting middleware).
- API là **MQTT client thứ 3** → tạo user MQTT riêng `apiserver` (`mosquitto_passwd`) + ACL tối thiểu
  (publish `smarthome/door/control`, subscribe topic cửa/face). Không dùng chung credential với WPF/bridge.
- Validate input ở biên (DTO + model validation); không lộ chi tiết lỗi nội bộ ra response.

## 6. Camera — MJPEG qua API (lưu ý tranh chấp)

Pi 5 chỉ có **1 camera**, vừa dùng cho nhận diện khuôn mặt (GĐ C) vừa cho mobile. **Không mở 2 tiến trình
`VideoCapture` cùng lúc** (lỗi "camera busy"). Giải pháp:

- **1 tiến trình capture duy nhất** trên Pi (vòng nhận diện) **fan-out frame**: vừa xử lý CV vừa đẩy frame
  ra nguồn MJPEG cục bộ (vd `mjpg-streamer` hoặc một endpoint nhỏ trong tiến trình Python).
- API **proxy** nguồn cục bộ đó tại `/api/camera/snapshot` (1 JPEG) và `/api/camera/stream` (MJPEG),
  bọc JWT để chỉ client đã đăng nhập xem được.
- Mobile mặc định **poll snapshot** (nhẹ pin/băng thông), nút "Xem trực tiếp" mới mở MJPEG stream.

## 7. Push notification

- **FCM** (Android) / **APNs** (iOS) cho thông báo khi app đóng. API lưu token thiết bị.
- Khi app đang mở (foreground) → đẩy realtime qua **SignalR hub** để hiển thị tức thì.
- Bảng mới **`mobile_push_tokens`**:

  | Cột | Kiểu | Ghi chú |
  |---|---|---|
  | `id` | bigserial PK | |
  | `user_id` | bigint FK → `users.id` | chủ thiết bị |
  | `token` | text | FCM/APNs token (unique) |
  | `platform` | varchar(10) | `android` \| `ios` |
  | `created_at` | timestamptz | |
  | `last_used_at` | timestamptz null | cập nhật khi gửi push thành công |

  > Đây là bảng **API ghi**, không đụng các bảng bridge ghi → không xung đột writer.

## 8. Triển khai

- API chạy **systemd service trên Pi 5** (.NET 8 runtime ARM64) cạnh broker + DB → MQTT/DB qua localhost,
  độ trễ thấp. (Tương tự `Pi5/systemd/smarthome-bridge.service`.)
- Cấu hình qua env/`.env` + appsettings (giống WPF): connection string, JWT key, MQTT credential `apiserver`,
  FCM server key, đường dẫn nguồn MJPEG cục bộ.

## 9. Lộ trình GĐ F (nối tiếp A–E) — việc cần làm chi tiết

**Tổng quan trạng thái:**

| Bước | Nội dung | Trạng thái |
|---|---|---|
| **F1** | Tách `Smart_Home.Core` + `Smart_Home.Contracts`; WPF/Tests trỏ sang; không đổi hành vi | ✅ **Xong** (2026-06-17) — build 0 lỗi, 47/47 test xanh |
| **F2** | Scaffold `Smart_Home.Api`: JWT login + `GET access-logs` + `POST door/unlock` | 🔜 Tiếp theo |
| **F3** | Camera proxy (snapshot + MJPEG) tái dùng camera Pi | ⏳ |
| **F4** | Push: MQTT subscriber → FCM/APNs + SignalR + bảng `mobile_push_tokens` | ⏳ |
| **F5** | `Smart_Home.Mobile` (MAUI): 4 màn hình | ⏳ |
| **F6** | Tailscale remote + hardening | ⏳ |

> Mỗi GĐ ghi rõ **việc cần làm (checkbox) · file/endpoint/bảng liên quan · tiêu chí hoàn thành (Done khi)**.

### 9.1. GĐ F1 — Tách lõi dùng chung ✅ HOÀN THÀNH (2026-06-17)

- [x] Tạo `Smart_Home.Core` (net8.0); `git mv` 15 Models + `Data/AppDbContext.cs` + 11 service thuần + MQTT client/topics (giữ namespace `Smart_Home.Models/.Data/.Service`).
- [x] Tạo `Smart_Home.Contracts` (net8.0) + DTO 4 chức năng: `AuthDtos`, `AccessLogDtos`, `DoorAndCameraDtos`.
- [x] WPF ref Core; gỡ `BCrypt.Net-Next` + `MQTTnet` direct (giờ transitive qua Core); giữ `NodePresenceService` ở WPF (`DispatcherTimer`/`App.Current`).
- [x] `Smart_Home.Tests` → `net8.0`, ref Core thay vì WPF.
- [x] Cập nhật `Smart_Home.slnx` (4 project).
- **File**: `Smart_Home.Core/*`, `Smart_Home.Contracts/*`, `Smart_Home/Smart_Home.csproj`, `Smart_Home.Tests/Smart_Home.Tests.csproj`, `Smart_Home.slnx`.
- ✅ **Done khi**: build 0 lỗi · 47/47 test xanh · namespace/XAML WPF không đổi · file là git rename. **(ĐÃ ĐẠT)**

### 9.2. GĐ F2 — Scaffold Web API + auth + 2 endpoint cốt lõi

- [ ] Tạo project `Smart_Home.Api` (`net8.0`, ASP.NET Core minimal hosting); ref `Smart_Home.Core` + `Smart_Home.Contracts`; thêm vào `Smart_Home.slnx`.
- [ ] `Program.cs`: cấu hình DI (`AddDbContextFactory<AppDbContext>` + Npgsql), JWT bearer auth, CORS, rate limiting, Swagger (chỉ DEV); nạp secrets từ env/`.env` (đồng bộ WPF).
- [ ] Đăng ký `IMqttService`/`MqttClientService` (Core) như singleton để publish; `ConnectAsync` lúc khởi động.
- [ ] **Auth**: `JwtTokenService` (ký access + refresh), `AuthController` → `POST /api/auth/login` (verify `users.password_hash` qua `IPasswordHasher` của Core), `POST /api/auth/refresh`. Trả `AuthResponse`.
- [ ] **Xem log**: `AccessLogsController` → `GET /api/access-logs?from&to&method&page&pageSize` → dùng `IAccessLogService` (Core) → map sang `PagedResult<AccessLogDto>`. `[Authorize]`.
- [ ] **Mở khóa**: `DoorController` → `POST /api/door/unlock` → role-gate → ghi `device_commands` + `IMqttService.PublishAsync(MqttTopics.DoorControl, {command:"unlock",duration,source:"mobile",by:userId})` → `UnlockResponse`. `[Authorize(Roles=...)]`.
- [ ] Mapping `Entity → DTO` (extension thuần hoặc Mapster/thủ công); validate query/body ở biên.
- [ ] Test: integration test tối thiểu cho login (sai/đúng) + access-logs (phân trang) bằng `WebApplicationFactory` + DB InMemory; unit test mapping.
- **File**: `Smart_Home.Api/Program.cs`, `Controllers/{Auth,AccessLogs,Door}Controller.cs`, `Auth/JwtTokenService.cs`, `Mapping/*`, `appsettings.json` + `appsettings.example.json`, `Smart_Home.Api.Tests/*` (tùy chọn).
- **Bảng/topic**: đọc `access_logs`, `users`, `roles`; ghi `device_commands`; publish `smarthome/door/control`.
- ✅ **Done khi**: gọi `login` lấy được JWT · `GET access-logs` trả dữ liệu thật phân trang · `POST door/unlock` (kèm JWT role hợp lệ) → message xuất hiện trên broker (kiểm bằng `mosquitto_sub`) · 401 khi thiếu/sai token.

### 9.3. GĐ F3 — Camera proxy (snapshot + MJPEG)

- [ ] Trên Pi: đảm bảo **1 nguồn MJPEG cục bộ** (vd `mjpg-streamer` hoặc endpoint trong tiến trình nhận diện) — **fan-out frame**, KHÔNG mở `VideoCapture` thứ 2.
- [ ] `CameraController`: `GET /api/camera/snapshot` (proxy 1 JPEG), `GET /api/camera/stream` (proxy `multipart/x-mixed-replace`), cả hai `[Authorize]`; `GET /api/camera/info` → `CameraInfoDto`.
- [ ] Cấu hình URL nguồn MJPEG qua appsettings/env; timeout + xử lý khi camera bận/offline (trả 503 + `Available=false`).
- [ ] Giới hạn số stream đồng thời (tránh nghẽn Pi).
- **File**: `Smart_Home.Api/Controllers/CameraController.cs`, `Services/MjpegProxy.cs`, config nguồn camera trên Pi (`Pi5/`).
- ✅ **Done khi**: mở `snapshot` thấy ảnh cửa hiện tại · `stream` xem được liên tục · nhận diện khuôn mặt vẫn chạy song song (không "camera busy").

### 9.4. GĐ F4 — Push notification + realtime

- [ ] **Khôi phục schema** `database/Smart_home_security_database.sql` (đang 0 byte) **trước** (xem mục 10), rồi thêm bảng `mobile_push_tokens` (mục 7) + migration EF (`AddMobilePushTokens`).
- [ ] `POST /api/devices/push-token` (`PushTokenRequest`) → lưu/cập nhật token theo `user_id`.
- [ ] `DoorEventsHostedService` (`BackgroundService`): subscribe `smarthome/face/result`, `door/rfid`, `door/keypad`, `door/breach` → dựng `DoorEventDto` → (a) gửi **FCM/APNs** tới token đã đăng ký, (b) đẩy **SignalR** cho client đang mở.
- [ ] `DoorEventsHub` (SignalR) + `GET /api/door/events?page` (lịch sử cho tab thông báo, đọc `alerts`/`access_logs`).
- [ ] Tích hợp FCM (server key trong `.env`); chống gửi trùng (debounce theo node/loại sự kiện).
- **File**: `Smart_Home.Api/HostedServices/DoorEventsHostedService.cs`, `Hubs/DoorEventsHub.cs`, `Controllers/{Devices,DoorEvents}Controller.cs`, `Services/PushSender.cs`, EF migration, cập nhật `database/Smart_home_security_database.sql`.
- **Bảng/topic**: ghi `mobile_push_tokens`; đọc `alerts`/`access_logs`; subscribe topic cửa.
- ✅ **Done khi**: có người ở cửa (giả lập bằng `Pi5/tools/simulate.py`) → điện thoại nhận push <vài giây · app đang mở thấy realtime qua SignalR · breach → push mức `critical`.

### 9.5. GĐ F5 — Mobile app MAUI (4 màn hình)

- [ ] Tạo `Smart_Home.Mobile` (.NET MAUI, `net8.0-android`/`net8.0-ios`); ref `Smart_Home.Contracts`; thêm vào solution.
- [ ] Hạ tầng: typed `HttpClient` (Refit tùy chọn) trỏ API qua tailnet; lưu JWT an toàn (`SecureStorage`); auto-refresh token; cấu hình base URL.
- [ ] **Màn Đăng nhập** → `POST /api/auth/login`, lưu token.
- [ ] **Màn Log ra/vào** → list `GET /api/access-logs` (phân trang, lọc theo ngày/method).
- [ ] **Màn Thông báo cửa** → đăng ký push token (`POST /api/devices/push-token`) + nhận FCM + list `GET /api/door/events`; kết nối SignalR khi mở.
- [ ] **Nút Mở khóa** → `POST /api/door/unlock` (xác nhận + phản hồi trạng thái).
- [ ] **Màn Camera** → poll `snapshot` mặc định + nút "Xem trực tiếp" mở MJPEG `stream`.
- **File**: `Smart_Home.Mobile/*` (Pages/ViewModels/Services), `MauiProgram.cs`.
- ✅ **Done khi**: đăng nhập → xem được log thật · nhận push khi simulate sự kiện cửa · bấm mở khóa → broker nhận lệnh · xem được camera. Không có chức năng quản trị thừa của WPF.

### 9.6. GĐ F6 — Remote access (Tailscale) + hardening

- [ ] Cài Tailscale trên Pi + điện thoại; API bind **tailnet (100.x) + localhost**; bật HTTPS (`tailscale serve`).
- [ ] Tạo user MQTT riêng `apiserver` (`mosquitto_passwd`) + ACL tối thiểu; cập nhật `.env` của API.
- [ ] Bật rate limiting cho `unlock` + login (chống brute-force); audit log cho mở khóa từ xa.
- [ ] Cài API thành **systemd service** trên Pi (`smarthome-api.service`, tương tự bridge); .NET 8 ARM64.
- [ ] Rà soát secrets (JWT key, FCM key, MQTT, DB) đều qua env/`.env`, không commit.
- **File**: `Pi5/systemd/smarthome-api.service`, `infra/broker-setup/` (ACL), tài liệu trong `SECURITY.md`.
- ✅ **Done khi**: điện thoại ngoài mạng nhà (qua tailnet) dùng đầy đủ 4 chức năng · API không truy cập được từ Internet công khai · mỗi client MQTT có credential riêng.

## 10. Việc cần làm trước/đi kèm (ngoài track F)

- ⚠️ `database/Smart_home_security_database.sql` đang **0 byte** — cần khôi phục schema (`pg_dump --schema-only`)
  trước khi thêm bảng `mobile_push_tokens`, để schema có "chỗ neo". (Xem `PHAN_TICH...` Phần 5, mục dọn dẹp.)
- ⚠️ Các mật khẩu DB/MQTT cũ đã lộ qua GitHub vẫn cần **xoay vòng** (SECURITY.md §3) trước khi mở thêm bề mặt API.
