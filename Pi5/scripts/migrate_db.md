# Moving the PostgreSQL database from the Windows PC to the Pi 5

The bridge and the WPF app share one PostgreSQL database. To make the Pi the
always-on hub, move the DB onto it and repoint the WPF app at the Pi.

## 1. Dump the schema (and optionally data) on the Windows PC

```powershell
# Schema only (recommended first move; the bridge re-creates rows as nodes report):
pg_dump --schema-only --no-owner --no-privileges -U postgres Smart_Home_db > smarthome_schema.sql

# Or full dump (schema + existing data):
pg_dump --no-owner --no-privileges -U postgres Smart_Home_db > smarthome_full.sql
```

> This also fixes the 0-byte `Smart_home_security_database.sql` problem noted in
> the roadmap: commit the regenerated `--schema-only` dump back to the repo.

## 2. Create the role + database on the Pi

```bash
sudo -u postgres psql <<'SQL'
CREATE ROLE app WITH LOGIN PASSWORD 'CHANGE_ME';
CREATE DATABASE "Smart_Home_db" OWNER app;
SQL
```

## 3. Restore on the Pi

```bash
psql -U app -d Smart_Home_db -h 127.0.0.1 -f smarthome_schema.sql   # or smarthome_full.sql
```

## 4. Allow LAN connections (only if the WPF PC must reach the Pi DB)

Edit `postgresql.conf`:  `listen_addresses = '*'`
Edit `pg_hba.conf`:      `host  Smart_Home_db  app  <LAN_subnet>/24  scram-sha-256`
Then: `sudo systemctl restart postgresql`

## 5. Point the bridge and WPF at the Pi

- Bridge: set `DATABASE_URL=postgresql://app:CHANGE_ME@127.0.0.1:5432/Smart_Home_db` in `Pi5/.env`.
- WPF: set the connection string (`.env` / user-secrets) `Host=<PI_LAN_IP>;Database=Smart_Home_db;Username=app;Password=CHANGE_ME`.
