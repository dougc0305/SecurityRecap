# SecurityRecap

## Product
SecurityRecap is a multi-tenant SaaS platform that ingests daily HOA security patrol 
report PDFs, uses the Anthropic Claude API to extract and analyze incident data, 
and delivers actionable intelligence via email summaries, a web dashboard, and 
a conversational AI chat interface.

## Tech Stack
- Backend: .NET 8 / C# / ASP.NET Core Web API
- Frontend: React + TypeScript + Vite
- Database: PostgreSQL
- ORM: Entity Framework Core
- AI: Anthropic Claude API (claude-opus-4-5)
- File Storage: Azure Blob Storage
- Auth: ASP.NET Identity + JWT
- Hosting: Azure Windows Server VM / IIS

## Repository Structure
SecurityRecap/
  src/
    SecurityRecap.Api/          # .NET 8 Web API
      Controllers/
      Services/
      Models/
      Data/
        Migrations/
      DTOs/
      Prompts/
    SecurityRecap.Core/         # Shared domain models and interfaces
    SecurityRecap.Infrastructure/ # EF Core, Azure Blob, email
  frontend/                # React + TypeScript + Vite
    src/
      components/
      pages/
      services/
      hooks/
      types/
  docs/
    PRD.md
  docker-compose.yml       # PostgreSQL local dev
  .gitignore

## Database Conventions
- All PostgreSQL table and column names are lowercase with underscores
- No quoting needed
- Use uuid for all primary keys
- Always include created_at timestamptz on every table

## Core Database Schema

### tenants
id, name, slug (unique), plan (hoa/management/security), is_active, created_at

### properties
id, tenant_id (FK), name, address, city, state, zip, 
security_company, report_email, timezone, is_active, created_at

### reports
id, property_id (FK), report_date, period_start, period_end,
raw_pdf_url, md_summary_url, ai_summary_html, officer_names[], created_at

### incidents
id, report_id (FK), property_id (FK), incident_time, incident_type
(noise/parking/maintenance/gate/law_enforcement/patrol/phone_call),
severity (low/medium/high/urgent), location, description, 
officer_name, law_enforcement (bool), case_number, created_at

### vehicles
id, property_id (FK), plate_number, plate_state, make, model, color,
first_seen, last_seen, violation_count, notes

### violations
id, incident_id (FK), vehicle_id (FK nullable), violation_type, 
location, notice_issued (bool), tow_notified (bool), created_at

### addresses_of_interest
id, property_id (FK), address, label, incident_count, 
first_flagged, last_incident, notes

### mailbox_ingest_configs
id, property_id (FK, unique), graph_tenant_id, graph_client_id,
graph_client_secret_protected (Data Protection, never returned over the API),
mailbox_address, folder_name, from_address, subject_contains,
attachment_name_contains, lookback_days, poll_interval_minutes, mark_as_read,
move_to_folder, send_summary_email, summary_recipients[], summary_subject_prefix,
is_enabled, last_polled_at, last_success_at, last_message_received_at,
last_error, consecutive_failures, created_at

### users
id, tenant_id (FK), email, full_name, role (admin/board_member/viewer),
is_active, created_at

### user_properties
user_id (FK), property_id (FK), receives_summary (bool, default false),
receives_alerts (bool, default false), PRIMARY KEY (user_id, property_id)

## API Conventions
- RESTful endpoints under /api/v1/
- All responses wrapped in { data, error, success }
- JWT bearer auth on all endpoints except /api/auth/*
- Tenant isolation enforced at service layer via TenantId claim
- Multi-tenant: every query filters by tenant_id

## Key API Endpoints

### Auth
POST /api/auth/login
POST /api/auth/refresh

### Ingest
POST /api/ingest/report  ← receives PDF + property_id, runs full pipeline

### Mailbox Ingest (admin-only)
GET    /api/v1/mailbox-ingest/{propertyId}
PUT    /api/v1/mailbox-ingest
DELETE /api/v1/mailbox-ingest/{propertyId}
POST   /api/v1/mailbox-ingest/{propertyId}/verify  ← test Graph credentials
POST   /api/v1/mailbox-ingest/{propertyId}/poll    ← run a poll immediately

### Properties
GET    /api/properties
POST   /api/properties
GET    /api/properties/{id}
PUT    /api/properties/{id}

### Reports
GET    /api/reports?propertyId=&page=&pageSize=
GET    /api/reports/{id}
GET    /api/v1/reports/{id}/pdf      ← original PDF
GET    /api/v1/reports/{id}/summary  ← generated Markdown summary file

### Incidents
GET    /api/incidents?propertyId=&type=&severity=&from=&to=&page=&pageSize=

### Vehicles
GET    /api/vehicles?propertyId=&plate=&page=&pageSize=
GET    /api/vehicles/{id}

### Chat
POST   /api/chat  ← { propertyId, message, conversationHistory[] }

## Claude API — Ingestion Prompt
Located at: src/SecurityRecap.Api/Prompts/IngestionPrompt.cs

The ingestion prompt receives:
- The PDF as base64
- The property's history as JSON, built by ReportHistoryContextBuilder: 90 days of incidents,
  counts by type over 7/30/90 days, repeat addresses, every known licence plate with its prior
  violation count and first-seen date, average incidents per report, and days since the last
  high/urgent incident

It returns a JSON object with:
- incidents[]
- vehicles[]
- maintenance_issues[]
- pattern_matches[]      (with first_observed, occurrence_count, significance)
- vehicles[]             (with seen_before, prior_violation_count, first_seen — informational;
                          the vehicles table remains authoritative for counts)
- anomalies[]            (expected-but-absent activity, values outside the norm)
- data_quality_notes[]   (internal contradictions in the source PDF)
- urgent_items[]
- html_summary (HTML string, no html/body tags)
- markdown_summary (stored as the report's summary file, md_summary_url)

## Claude API — Chat Prompt
Located at: src/SecurityRecap.Api/Prompts/ChatPrompt.cs

The chat prompt receives:
- User message
- Full conversation history
- Last 90 days of incidents as structured JSON
- Aggregated property statistics

## Frontend Pages
- /login
- /dashboard          ← overview, incident counts, recent activity, flags
- /incidents          ← timeline with filters
- /vehicles           ← plate tracker
- /addresses          ← address watch list
- /reports            ← archive with PDF download and AI summary view
- /chat               ← AI chat interface
- /settings           ← property and user management

## Licence plates
Use SecurityRecap.Core.PlateNumber for anything plate-related. Reports use UNKNOWN / N/A / NONE
to mean "tag not readable"; recorded as vehicles these collect into one fictitious row that
looks like a serial offender and poisons repeat-plate history. IngestionService records such a
violation with VehicleId null instead, and the history context excludes them.

## Automated Report Pickup
Patrol report emails are picked up from a mailbox via Microsoft Graph (app-only) and pushed
through the same IIngestionService path as a manual upload. See docs/MailboxIngestion.md for
the Entra app registration, per-property setup, and deployment prerequisites.

- MailboxPollingBackgroundService ticks on MailboxIngest:TickIntervalSeconds and polls each
  property whose own interval has elapsed; failures back off exponentially.
- Poll cadence is time-of-day aware: active_window_poll_minutes inside the daily window the
  report is expected, poll_interval_minutes outside it. The window is evaluated in
  schedule_time_zone (IANA), which falls back to the property's timezone but is stored
  separately — the property is where the patrol happens, not necessarily where the sender's
  schedule is anchored. Palm Cove is America/Chicago while its report is sent on Eastern time.
- Idempotency: report external_id is `graph:{internetMessageId}:{fileName}`.
- Failure alerts (MailboxAlertNotifier) email users flagged with receives_alerts when a poll
  fails, a report cannot be processed, or no report has arrived within stale_after_hours.
  Repeats of the same problem are throttled to 12h; a recovery notice is sent when it clears.
  NOTE: this is in-process, so it cannot report that the app itself is down — that is what the
  IIS app pool settings below guard against.
- Summary recipients are resolved at send time, never stored as a list: active users with
  user_properties.receives_summary for that property, plus mailbox_ingest_configs
  .summary_recipients for people with no account. Deactivating a user or removing their
  property assignment stops their mail on the next report.
- The poller has no signed-in user; it calls IngestReportAsync with the property's own
  tenant_id, Guid.Empty user id, and UserRole.Admin.
- Integration secrets use ISecretProtector (ASP.NET Data Protection), NOT the one-way hashing
  used for passwords and API keys, because they must be replayed to Graph.

## IIS requirements for background polling
MailboxPollingBackgroundService only runs while the app pool is alive, so the SecurityRecap
app pool MUST be configured to stay up. Defaults will silently stop all polling:
- processModel.idleTimeout = 00:00:00 (default 20 min terminates the worker, killing the poller)
- startMode = AlwaysRunning, and preloadEnabled = true on the app
- recycling.periodicRestart.time = 0 with a fixed schedule (03:00) instead of the drifting 29h default

## Environment Variables (.env / appsettings)
- ConnectionStrings__DefaultConnection  (PostgreSQL)
- Anthropic__ApiKey
- Azure__BlobStorage__ConnectionString
- Azure__BlobStorage__ContainerName
- Jwt__Secret
- Jwt__Issuer
- Jwt__Audience
- Email__SmtpHost / Email__SmtpPort / Email__FromAddress
- DataProtection__KeyPath        (MUST be outside the deploy root — deploy.bat moves it)
- LocalStorage__RootPath         (MUST be outside the deploy root — deploy.bat moves it)
- MailboxIngest__Enabled / MailboxIngest__TickIntervalSeconds

## Development Notes
- Use docker-compose for local PostgreSQL on port 5432
- EF Core migrations via dotnet ef
- Vite dev server proxies /api to .NET backend
- All dates stored as UTC, but the history handed to Claude is rendered in the PROPERTY's
  local time with a time_zone field, because patrol reports print local times. Passing UTC
  there makes the model compare a local time in the PDF against a UTC timestamp and invent an
  offset-sized "anomaly" — it once reported the gate opening 5 hours early when nothing had
  changed. Patrol reports carry local times with an offset, so
  parse with AdjustToUniversal|AssumeUniversal — relabelling a local time as UTC shifts every
  incident and corrupts the history comparison windows.
- Migrations run automatically only in Development (DbSeeder). Apply them to production by hand.
- Property timezone used only for display formatting
