# Database — schema PostgreSQL

Schema gốc của hệ thống Smart Home. **App WPF (`Smart_Home/`) và bridge Pi 5 (`Pi5/`) dùng chung
một schema này** — đây là nguồn chân lý duy nhất cho cấu trúc bảng.

## File

| File | Nội dung |
|---|---|
| `Smart_home_security_database.sql` | Dump schema (`pg_dump --schema-only`) của database `Smart_Home_db`. |

> ⚠️ **Hiện file đang 0 byte** — schema gốc đã mất khỏi repo. Cần dump lại từ DB thật (lệnh dưới)
> rồi commit. EF Core trong app là code-first (`Smart_Home/Data/AppDbContext.cs`, 12 `DbSet`) nên
> đây là tài liệu tham chiếu, không phải nguồn tạo bảng.

## Dump lại schema

```powershell
# Trên máy có DB (PC Windows):
pg_dump --schema-only --no-owner --no-privileges -U postgres Smart_Home_db > database/Smart_home_security_database.sql
```

```bash
# Bản đầy đủ (schema + dữ liệu), nếu cần seed:
pg_dump --no-owner --no-privileges -U postgres Smart_Home_db > database/Smart_home_full.sql
```

Triển khai sang Pi 5 (restore schema này lên DB của Pi): xem `../Pi5/scripts/migrate_db.md`.
