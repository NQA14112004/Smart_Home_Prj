# Bảo mật & quản lý secrets — Smart Home Security

Tài liệu này mô tả cách cấu hình secrets (mật khẩu DB, thông tin MQTT) và bật MQTT auth + TLS sau khi
áp dụng các cải tiến §1.4. **Không commit secret thật vào source.**

## 1. Secrets đã được đưa ra khỏi source

- `appsettings.json` không còn chứa mật khẩu PostgreSQL (đã để trống `Password=`).
- Thứ tự ưu tiên đọc connection string (trong `App.xaml.cs`):
  1. Biến môi trường `SMART_HOME_CONNECTION_STRING`
  2. Biến môi trường `ConnectionStrings__DefaultConnection`
  3. .NET user-secrets / `appsettings.json` (qua `IConfiguration`)
- MQTT credentials: biến môi trường `MQTT_USERNAME` / `MQTT_PASSWORD` được ưu tiên hơn `appsettings.json`.

## 2. Thiết lập cho máy dev (dotnet user-secrets)

Chạy trong thư mục `Smart_Home/` (csproj đã có `<UserSecretsId>smart-home-wpf-secrets</UserSecretsId>`):

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Smart_Home_db;Username=postgres;Password=<MAT_KHAU_MOI>"
dotnet user-secrets set "MqttOptions:Username" "wpfclient"
dotnet user-secrets set "MqttOptions:Password" "<MAT_KHAU_MQTT>"
```

Secrets nằm tại `%APPDATA%\Microsoft\UserSecrets\smart-home-wpf-secrets\secrets.json` (ngoài source).
Trên server/production dùng **biến môi trường** thay cho user-secrets.

## 3. ⚠️ Xoay vòng mật khẩu PostgreSQL đã lộ

Mật khẩu cũ (`14112004`) đã từng nằm trong source nên coi như **đã lộ — phải đổi**:

```sql
ALTER ROLE postgres WITH PASSWORD '<MAT_KHAU_MOI>';
```

Sau đó cập nhật lại user-secret/biến môi trường ở mục 2 bằng mật khẩu mới.

## 4. Bật MQTT auth + TLS trên Mosquitto (Raspberry Pi 5 / broker host)

### Tạo CA + chứng chỉ server (self-signed)

```bash
openssl req -new -x509 -days 3650 -nodes -keyout ca.key -out ca.crt -subj "/CN=SmartHomeCA"
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr -subj "/CN=<broker-host-or-ip>"
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out server.crt -days 825
mosquitto_passwd -c /mosquitto/config/passwd wpfclient
```

### `mosquitto.conf`

```conf
allow_anonymous false
password_file /mosquitto/config/passwd
listener 8883
cafile  /mosquitto/config/ca.crt
certfile /mosquitto/config/server.crt
keyfile  /mosquitto/config/server.key
```

> `CN` của server cert phải khớp host/IP mà WPF kết nối tới (`MqttOptions:Host`).

### Cấu hình phía WPF

Sao chép **`ca.crt` (chỉ phần public)** sang máy chạy WPF, rồi đặt trong user-secrets/appsettings:

```json
"MqttOptions": {
  "Host": "<broker-host>",
  "UseTls": true,
  "TlsPort": 8883,
  "CaCertPath": "certs/ca.crt"
}
```

- Khi `UseTls=true` và có `CaCertPath`: client **chỉ tin** CA này (pin theo `ValidateAgainstCa`), chống MITM.
- `AllowUntrustedCertificates=true` chỉ dùng cho DEV (tắt xác thực broker — KHÔNG dùng production).
- Khóa riêng `ca.key`/`server.key`/`server.crt` **không bao giờ** rời broker; chỉ phân phối `ca.crt`.

## 5. Kiểm tra nhanh

- `grep -r 14112004` trong source và `bin/` → không còn kết quả.
- `UseTls=false`: app vẫn kết nối cổng 1883 như cũ (regression).
- `allow_anonymous false` + không có credential → kết nối **thất bại** (đúng).
- `UseTls=true` + `CaCertPath` đúng → kết nối thành công; trỏ sai CA → bị từ chối (CA thực sự được enforce).
