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

### users
id, tenant_id (FK), email, full_name, role (admin/board_member/viewer),
is_active, created_at

### user_properties
user_id (FK), property_id (FK), PRIMARY KEY (user_id, property_id)

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

### Properties
GET    /api/properties
POST   /api/properties
GET    /api/properties/{id}
PUT    /api/properties/{id}

### Reports
GET    /api/reports?propertyId=&page=&pageSize=
GET    /api/reports/{id}

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
- Last 30 days of incidents as JSON history context

It returns a JSON object with:
- incidents[]
- vehicles[]
- maintenance_issues[]
- pattern_matches[]
- html_summary (HTML string, no html/body tags)

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

## Environment Variables (.env / appsettings)
- ConnectionStrings__DefaultConnection  (PostgreSQL)
- Anthropic__ApiKey
- Azure__BlobStorage__ConnectionString
- Azure__BlobStorage__ContainerName
- Jwt__Secret
- Jwt__Issuer
- Jwt__Audience
- Email__SmtpHost / Email__SmtpPort / Email__FromAddress

## Development Notes
- Use docker-compose for local PostgreSQL on port 5432
- EF Core migrations via dotnet ef
- Vite dev server proxies /api to .NET backend
- All dates stored and returned as UTC
- Property timezone used only for display formatting
