# Deploying AcxiomCRM

AcxiomCRM is a single ASP.NET Core 8 app with an embedded SQLite database. On start-up it:

1. creates the data directory if needed,
2. applies pending EF Core migrations,
3. creates the `Admin`, `Manager` and `SalesExecutive` roles,
4. creates any users listed under `Seed:Users` that don't exist yet.

There is no separate database server to provision.

## What gets stored where

| Data | Default location | Override with |
|---|---|---|
| SQLite database | `<content root>/App_Data/acxiomcrm.db` | `ConnectionStrings__DefaultConnection` |
| Cookie encryption keys | next to the database, in `keys/` | `DataProtection__KeysPath` |

**Back up both.** If the keys are lost, every user is signed out. If the database is lost, all data is gone.

## Configuration

All settings can be supplied as environment variables. Use `__` for nesting.

| Variable | Default | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Never use `Development` on a server. It shows detailed errors and seeds demo accounts. |
| `ConnectionStrings__DefaultConnection` | `DataSource=App_Data/acxiomcrm.db;Cache=Shared` | Database file location |
| `Seed__Users__0__Email` / `__Password` / `__FullName` / `__Role` | — | First administrator, created on first start only. Change the password after the first sign-in. |
| `Seed__SampleData` | `false` | Load demo customers, leads and opportunities |
| `Security__RequireHttps` | `true` | HTTPS-only cookies, HSTS and redirection |
| `ReverseProxy__Enabled` | `false` | Trust `X-Forwarded-For`/`-Proto`. Enable only when the app is reachable solely through the proxy. |
| `Security__MaxFailedAccessAttempts` | `5` | Failed sign-ins before lockout |
| `Security__LockoutMinutes` | `15` | Lockout duration |
| `Security__AuthRequestsPerMinute` | `10` | Login rate limit per IP address |

The health endpoint, `GET /health`, returns `Healthy` when the app and database are reachable. Point load-balancer and uptime checks at it.

---

## Option A: Docker (recommended)

```bash
cp .env.example .env          # set ADMIN_EMAIL and ADMIN_PASSWORD
docker compose up -d --build
```

The app listens on port 8080 inside the container, mapped to `HOST_PORT` (default 8080). Data lives in the `acxiomcrm-data` volume.

To try it locally over plain HTTP, set `Security__RequireHttps=false` in `.env`. On a server, keep it `true` and put HTTPS in front (Option C).

Useful commands:

```bash
docker compose logs -f acxiomcrm   # follow the logs
docker compose pull && docker compose up -d --build   # redeploy; data volume is kept
docker run --rm -v acxiomcrm_acxiomcrm-data:/data -v "$PWD":/backup alpine tar czf /backup/acxiomcrm-backup.tgz -C /data .   # back up the volume
```

## Option A2: Render (managed Docker hosting)

1. Go to **render.com → New → Web Service** and connect `cherrieee24/AcxiomCRM`. The runtime is detected as **Docker** from the `Dockerfile`.
2. Choose the **Singapore** region (closest to India) and the `main` branch.
3. Add these **environment variables**:

   | Key | Value |
   |---|---|
   | `PORT` | `8080` |
   | `ReverseProxy__Enabled` | `true` |
   | `Seed__Users__0__Email` / `__Password` / `__FullName` | the administrator account |
   | `Seed__Users__0__Role` | `Admin` |
   | `Seed__SampleData` | `true` for a demo environment |
   | `Seed__Users__1__*` | a Manager (`Role=Manager`) |
   | `Seed__Users__2__*`, `Seed__Users__3__*` | Sales Executives (`Role=SalesExecutive`, `ManagerEmail=` the manager's email) |

4. Under **Advanced**:
   - set the **Health Check Path** to `/health`;
   - on a paid instance, add a **Disk** mounted at `/app/data` (1 GB is plenty).
5. Click **Create Web Service**. The first build takes about 5–8 minutes. Later pushes to `main` redeploy automatically.

On the **free** instance the filesystem is temporary. The database resets whenever the service sleeps (after about 15 idle minutes), restarts or redeploys. With `Seed__SampleData=true` and the demo users configured, each start gives a fresh, fully populated demo.

## Option B: Publish folder (Linux VM, Windows/IIS, Azure App Service)

```bash
scripts/publish.sh            # outputs to artifacts/publish
```

Copy `artifacts/publish/` to the server, which needs the **ASP.NET Core 8 runtime**, and run:

```bash
ASPNETCORE_ENVIRONMENT=Production \
Seed__Users__0__Email=admin@yourcompany.com Seed__Users__0__Password='<strong password>' Seed__Users__0__Role=Admin \
dotnet AcxiomCRM.dll
```

Platform notes:
- **IIS:** install the ASP.NET Core Hosting Bundle and point the site at the publish folder. The app pool identity needs write access to `App_Data`.
- **Azure App Service (Linux):** deploy the publish folder or the Docker image. Set the variables above as App Settings, and set `ConnectionStrings__DefaultConnection=DataSource=/home/data/acxiomcrm.db` so the database sits on persistent storage.

## Option C: Behind nginx with HTTPS (Linux VM)

1. Run the app as a systemd service, or with Docker, on `127.0.0.1:8080`.
2. Set `ReverseProxy__Enabled=true`.
3. Get a certificate, for example `certbot --nginx -d crm.yourcompany.com`.
4. Use an nginx site like:

```nginx
server {
    listen 443 ssl;
    server_name crm.yourcompany.com;
    # ssl_certificate / ssl_certificate_key managed by certbot

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

Example systemd unit, `/etc/systemd/system/acxiomcrm.service`:

```ini
[Unit]
Description=AcxiomCRM
After=network.target

[Service]
WorkingDirectory=/opt/acxiomcrm
ExecStart=/usr/bin/dotnet /opt/acxiomcrm/AcxiomCRM.dll
Restart=always
User=acxiomcrm
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:8080
Environment=ReverseProxy__Enabled=true
EnvironmentFile=/etc/acxiomcrm.env

[Install]
WantedBy=multi-user.target
```

Put the `Seed__Users__0__*` values in `/etc/acxiomcrm.env` with permissions `chmod 600`.

## Pre-go-live checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Served over HTTPS, with `Security__RequireHttps=true`
- [ ] First admin created, then its password changed through the UI
- [ ] Seed admin password removed from the environment after first start (optional but recommended)
- [ ] Database and keys directory on persistent storage, and backed up
- [ ] `/health` monitored
- [ ] `ReverseProxy__Enabled=true` only when behind a proxy
