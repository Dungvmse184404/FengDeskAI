# FengDeskAI

An e-commerce platform for **feng-shui desk decoration products**, with an AI-powered
recommendation engine and a tool-calling conversational assistant.

Users describe their workspace (element/mệnh, desk orientation, lighting, purpose…); a
**deterministic feng-shui scoring engine** in .NET ranks products, and an AI layer only
*explains* the result persuasively — it never invents rules or adds/removes products.

> SEP490 capstone — code `SU26SE093`. See [Documents/PROPOSAL.md](Documents/PROPOSAL.md)
> for the full proposal, feature status, and roadmap.

---

## Architecture

Clean Architecture (4 layers + a shared Contracts project):

```
WebAPI  ──►  Application  ──►  Domain
   │              │
   └──►  Infrastructure ◄──────┘
                  │
            Contracts  (DTOs shared with the AI recommendation microservice)
```

- **Domain** — entities, enums, pure business rules.
- **Application** — feature folders by bounded context (Identity, Workspace, Catalog,
  Vendor, Geography, Sales, Payment, Shipping, Chat, CustomerCare, Announcement) +
  the feng-shui engine.
- **Infrastructure** — EF Core/Npgsql, external integrations (PayOS, Supabase Storage,
  Ollama, Meshy, SMTP).
- **WebAPI** — controllers, SignalR hub, background workers, authorization policies.
- **Contracts** — request/response contract for the Python AI recommendation service.

## Tech stack

| Area | Technology |
|------|------------|
| Backend | .NET 8, ASP.NET Core Web API |
| ORM / DB | EF Core 8 + Npgsql · PostgreSQL (Supabase) |
| Realtime | SignalR (`/hubs/chat`) |
| Auth | JWT Bearer + refresh token, email OTP (MailKit/SMTP) |
| Payments | PayOS (+ COD) |
| Image storage | Supabase Storage |
| Conversational LLM | Ollama (qwen3.5, gemma3…) with tool-calling |
| 3D model generation | Meshy (image-to-3D) — currently mocked |
| Frontend | React (separate repo: `FengDeskAI_FE`) |
| Deployment | Docker · Railway |

## Project structure

```
src/
  FengDeskAI.Domain/          # entities, enums
  FengDeskAI.Application/      # features, services, feng-shui engine, interfaces
  FengDeskAI.Infrastructure/   # EF Core, migrations, external clients, seeders
  FengDeskAI.Contracts/        # AI recommendation DTOs + CONTRACT.md
  FengDeskAI.WebAPI/           # controllers, SignalR hubs, workers, Program.cs
Documents/                     # diagrams, PROPOSAL.md
Dockerfile · docker-compose.yml · .env.example
```

---

## Getting started (local)

### Prerequisites
- .NET 8 SDK
- A PostgreSQL database (e.g. Supabase) — connection string
- (Optional) An Ollama endpoint for the AI chat

### Configure

Secrets are **not** committed. `appsettings.json` ships with safe, blank defaults;
real values come from environment variables (`Section__Key` convention) or a local
`.env` file.

```bash
cp .env.example .env      # then fill in real values
```

Key variables (see [.env.example](.env.example) for the full list):

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `JwtSettings__SecretKey` | JWT signing key |
| `MailSettings__Username` / `__Password` | SMTP (Gmail app password) |
| `PayOSSettings__ClientId` / `__ApiKey` / `__ChecksumKey` | PayOS |
| `SupabaseStorage__ApiKey` | Supabase Storage service key |
| `AiChat__BaseUrl` | Ollama / LLM endpoint |
| `AiRecommendationSettings__ApiKey` | Internal key for the Python AI service |

### Run

```bash
# apply migrations + seed reference data, then exit
dotnet run --project src/FengDeskAI.WebAPI -- seed

# run the API (Swagger at /swagger)
dotnet run --project src/FengDeskAI.WebAPI
```

---

## Deployment (Docker / Railway)

The app is containerized and ready to deploy.

```bash
# Build + run the API on port 8080
docker compose up -d --build

# One-shot: apply migrations + seed, then exit
docker compose run --rm migrate
```

The `Dockerfile` is a multi-stage build (.NET 8), runs as a non-root user, and binds to
Railway's dynamic `$PORT` when present (falls back to `8080` locally).

### Railway
1. **Variables** → *Raw Editor* → paste the contents of your `.env`.
2. Railway auto-detects the `Dockerfile`, builds, and deploys.
3. Run migrations once via `railway run dotnet FengDeskAI.WebAPI.dll seed` (or
   temporarily set the start command to `… seed`).

> **Security:** rotate any secret that was previously stored in plaintext before going
> to production. Configuration is layered so env vars always override
> `appsettings.json` at runtime — no secrets are baked into the image.

---

## Feature status (summary)

**Done** — auth/OTP, workspace profiles, catalog + feng-shui attributes, deterministic
recommendation engine, AI chat with 8 tools, realtime chat + support rooms + consent,
cart/order/payment (PayOS + COD), shipping webhook, multi-store entities, notifications,
reviews, Docker/Railway deploy.

**Not yet implemented** — Python AI recommendation service (currently mocked), real 3D
generation (Meshy mocked), after-sales/returns, structured support tickets, analytics
dashboard, real carrier integration, automated tests & CI/CD.

See [Documents/PROPOSAL.md](Documents/PROPOSAL.md) §7 for details and the proposed roadmap.
