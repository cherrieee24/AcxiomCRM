# AcxiomCRM

A role-based CRM built with **ASP.NET Core 8 MVC**, **ASP.NET Core Identity**, **Entity Framework Core (SQLite)**, **Bootstrap 5** and **Chart.js**. It covers the full sales lifecycle: lead capture, qualification and conversion, opportunity pipeline, follow-ups and activities. On top of that it provides secure authentication, role-based authorization, audit logging and a REST API.

## Repository layout

```
ACXIOM/
├── src/AcxiomCRM/            The web application (ASP.NET Core MVC + REST API)
│   ├── Controllers/          MVC controllers (thin: bind → call service → render)
│   │   └── Api/              REST API controllers (/api/...)
│   ├── Data/                 DbContext (audit + soft delete), seeder, EF migrations
│   ├── Dtos/                 Input models (shared by forms + API, carry validation) and response DTOs
│   ├── Infrastructure/       Roles, permission matrix, validation rules, paging, UI helpers
│   ├── Models/               Entities
│   ├── Services/             Business logic, data scoping, auth, audit, dashboard
│   ├── ViewModels/           Page models
│   ├── Views/                Razor views
│   ├── wwwroot/              Static assets (css, js, libs)
│   ├── App_Data/             Local SQLite database + keys (git-ignored, created at runtime)
│   ├── appsettings.json              Base settings
│   ├── appsettings.Development.json  Demo accounts + sample data (never published)
│   └── appsettings.Production.json   Production overrides
├── docs/DEPLOYMENT.md        Docker / VM / IIS / Azure / nginx deployment guide
├── scripts/                  run-dev.sh, publish.sh
├── .github/workflows/ci.yml  Build, test and Docker build on every push
├── Dockerfile                Multi-stage image (non-root, data on a volume)
├── docker-compose.yml        Single-server deployment
├── .env.example              Template for deployment secrets (.env is git-ignored)
├── global.json               Pins the .NET 8 SDK
└── AcxiomCRM.sln
```

## Running locally (development)

Prerequisite: .NET 8 SDK (installed here at `~/.dotnet`).

```bash
export PATH="$HOME/.dotnet:$PATH"
scripts/run-dev.sh
```

Then open http://localhost:5063.

In Development the app seeds demo users for every role, plus sample CRM data. The demo accounts are listed in `src/AcxiomCRM/appsettings.Development.json` under `Seed:Users`. That file is excluded from publish output and Docker images.

To reset local data, delete `src/AcxiomCRM/App_Data/` and restart.

## Deploying

```bash
cp .env.example .env      # set the first admin's email and password
docker compose up -d --build
```

Or run `scripts/publish.sh` and copy `artifacts/publish/` to a server. See **[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** for Docker, Linux + nginx + HTTPS, IIS and Azure App Service, the full configuration reference, backups and a go-live checklist.

Production behaviour:
- Migrations run automatically at start-up.
- No demo data or demo accounts.
- HTTPS-only cookies plus HSTS.
- Keys persist so sign-ins survive restarts.
- Health check at `GET /health`.
- Optional reverse-proxy support.

## Mandatory module tree (§17.2): where to find each item

| Module | Items | Location |
|---|---|---|
| Authentication | Login, Register, Logout, Access Control | `/Account/Login`, `/Account/Register`, user menu → Sign out; roles + data scoping on every request |
| Dashboard | Total Customers, Total Leads, Open/Won/Lost Opportunities, Sales Pipeline, Charts | `/` (KPI cards, pipeline value, Chart.js charts) |
| Customer Management | Create, Edit, Details, Delete, Search | `/Customers` |
| Lead Management | Create, Edit, Details, Delete, Lead Status, Lead Conversion | `/Leads` (status workflow on Details, **Convert** for Qualified leads) |
| Opportunity Management | Create, Edit, Details, Delete, Sales Pipeline | `/Opportunities`, `/Opportunities/Pipeline` |
| Follow-Up | Schedule, Complete, Pending Follow-Ups | Follow-Ups menu → Schedule / **Pending Follow-Ups** (`/FollowUps/Pending`) |
| Activity Management | Call, Meeting, Email, Task | Activities menu → Calls / Meetings / Emails / Tasks |
| User & Role Management | Users, Roles, Permissions | Administration → Users / Roles / Permissions (Admin only) |
| Audit Log | Login, Create, Update, Delete | Administration → Audit Log (Login / Create / Update / Delete tabs) |

## How the requirements are met

| Area | Implementation |
|---|---|
| Authentication | Identity cookie auth: Login / Register / Logout / Change Password. The fallback authorization policy protects every page by default. |
| Password security | Identity password hashing. Policy: at least 8 characters with upper case, lower case, a digit and a symbol (configurable under `Security` in appsettings). |
| Lockout | 5 failed attempts → 15 min lockout (configurable). Admin can unlock. Login endpoints are also rate limited. |
| Authorization | `[Authorize(Roles=…)]` plus **data scoping** in services: Admin sees all, Manager sees own + team (direct reports) + unassigned, Sales Executive sees own. Records outside the user's scope return 404, even when the URL is edited by hand. |
| Anti-forgery | A global `AutoValidateAntiforgeryToken` filter, plus `[ValidateAntiForgeryToken]` on every POST action. |
| SQL injection | EF Core LINQ only; no raw SQL. |
| Client validation | Unobtrusive validation (required, email, phone, length, date, numeric/range) and a custom `notpast` rule. |
| Server validation | Data annotations re-run inside the services, plus business rules (uniqueness, status transitions, scope checks on foreign keys). |
| Business rules | Amount > 0; probability 0–100; close date not in the past while an opportunity is open; follow-up date not before today; unique customer email and phone; no duplicate customer (same name + company); valid lead status transitions. |
| Audit log | Every create, update and delete on CRM entities is captured automatically in `SaveChangesAsync`, with old/new values, user and IP. Auth and security events are logged explicitly. Audit rows are append-only (enforced by the DbContext). |
| Dashboard | Role-scoped KPI cards, date-range filter, and Chart.js charts for lead status, opportunity pipeline and monthly sales. |
| REST API | `/api/customers` (full CRUD), `/api/leads` (+ convert), `/api/opportunities`, `/api/followups` (+ complete), `/api/reports/pipeline`, `/api/auth/login`, `/api/auth/logout`. Uses DTOs and ProblemDetails errors, with 200/201/204/400/401/403/404/409/423 status codes. |

## Using the API

The API uses the same Identity cookie as the web app. Sign in first, then reuse the cookie:

```bash
curl -c jar.txt -H "Content-Type: application/json" \
     -d '{"email":"<user>","password":"<password>"}' http://localhost:5063/api/auth/login
curl -b jar.txt "http://localhost:5063/api/customers?search=infosys&page=1&pageSize=10"
```

## Not built yet

- Dedicated **Reports** pages (customer, lead, follow-up, conversion, user activity and audit reports, with export). Pipeline data is already available at `/api/reports/pipeline`.
- Automated tests (xUnit + WebApplicationFactory).
