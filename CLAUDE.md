# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run full stack locally via Aspire (preferred for development)
dotnet run --project src/Orchestrator.AppHost

# Run with plain docker-compose (no Aspire dashboard)
cp .env.template .env   # fill in values first
docker compose up --build

# Build entire solution
dotnet build Orchestrator.slnx

# Run all tests — starts Docker Desktop automatically if not running
.\test.ps1

# Run without Docker (integration tests skip automatically)
dotnet test Orchestrator.slnx

# Add an EF Core migration
dotnet ef migrations add <Name> --project src/Orchestrator.Infrastructure --startup-project src/Orchestrator.Api
```

## Architecture

7 projects across two layers:

### Application projects (Clean Architecture)

| Project | Role |
|---|---|
| `Orchestrator.Core` | Domain entities (`Tenant`, `Project`, `Requirement`, `BusinessContext`), interfaces (`IHybridRagService`, `IGuardrailService`) |
| `Orchestrator.Infrastructure` | EF Core `AppDbContext`, `HybridRagService`, `GuardrailService`, `AppDbContextFactory` (migrations), `DependencyInjection.AddInfrastructureServices` |
| `Orchestrator.Engine` | Semantic Kernel plugin (`BusinessContextPlugin`) exposing RAG search as a `[KernelFunction]` |
| `Orchestrator.Api` | ASP.NET Core controllers, `GuardrailMiddleware`, entry point |
| `Orchestrator.Tests` | xUnit integration tests via Testcontainers (`pgvector/pgvector:pg16`) |

### Aspire projects

| Project | Role |
|---|---|
| `Orchestrator.AppHost` | Aspire orchestrator — starts the pgvector container and the API, wires the `orchestrator` connection string, opens the Aspire dashboard |
| `Orchestrator.ServiceDefaults` | Shared defaults: OpenTelemetry (traces/metrics/logs), health checks (`/health`, `/alive`), service discovery, HTTP resilience |

Dependency direction: `AppHost` → `Api` → `Engine` → `Core` ← `Infrastructure` ← `Api`

## Key Design Decisions

**Hybrid Storage:** PostgreSQL is the single source of truth for both relational data and vectors. `BusinessContext.Embedding` is a `vector(1536)` column (pgvector). Hybrid queries combine `WHERE TenantId = @id` (relational filter) with `ORDER BY embedding <-> @query` (L2 vector distance) — implemented in `HybridRagService.SearchAsync`.

**Embedding API:** Uses `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>` registered via `AddOpenAIEmbeddingGenerator`. The `SKEXP0010` diagnostic is suppressed in `Orchestrator.Infrastructure.csproj` because this API is still experimental in SK 1.75.

**Aspire DB integration:** `builder.AddNpgsqlDbContext<AppDbContext>("orchestrator")` in `Program.cs` handles connection string injection from Aspire (or from env var `ConnectionStrings__orchestrator` in non-Aspire mode). `UseVector()` is configured in `configureDbContextOptions`. This gives automatic retries, health checks, and DB traces for free.

**Guardrail Pipeline:** `GuardrailMiddleware` buffers the response body only for `/api/generate/*` routes, validates via `IGuardrailService`, and returns HTTP 422 on violation. All other routes pass through without buffering.

**Auto-migration:** `Program.cs` runs `MigrateAsync()` on startup — the schema (including `CREATE EXTENSION vector`) is always current. `AppDbContextFactory` provides a design-time connection for `dotnet ef` commands without a live application.

**Dual-mode transport:** The same API and connection string key (`orchestrator`) work under both Aspire AppHost (which injects it) and plain `docker-compose` (which sets `ConnectionStrings__orchestrator`).

**AppHost SDK:** `Orchestrator.AppHost.csproj` uses the multi-SDK style (`<Sdk Name="Aspire.AppHost.Sdk" Version="13.3.0" />`) — the workload-based `IsAspireHost` property is deprecated in .NET 10 and causes `NETSDK1228`.

## Environment Variables

| Variable | Purpose |
|---|---|
| `ConnectionStrings__orchestrator` | Npgsql connection string (injected by Aspire; set manually for docker-compose) |
| `OpenAI__ApiKey` | OpenAI API key for `text-embedding-ada-002` embeddings |
| `DATABASE_PASSWORD` | Postgres password (docker-compose only) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional — enables OTLP export of traces and metrics |

## Running Locally

**With Aspire (recommended):**
```bash
dotnet run --project src/Orchestrator.AppHost
```
The Aspire dashboard opens automatically. The pgvector container starts, connection strings are injected, and the API migrates on first boot.

**With docker-compose (CI / no Aspire):**
```bash
cp .env.template .env   # set DATABASE_PASSWORD and OPENAI_API_KEY
docker compose up --build
```
API: `http://localhost:8080` — OpenAPI: `http://localhost:8080/openapi/v1.json`
