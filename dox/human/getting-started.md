# Getting Started

## Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 10 SDK** | The solution targets `net10.0`. Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download). Verify with `dotnet --version` — you need `10.x.x`. |
| **Docker Desktop** | Required for the pgvector database container. The Aspire path and the test runner both start containers automatically. Download from [docker.com](https://www.docker.com/products/docker-desktop/). |
| **OpenAI API key** | The embedding service calls `text-embedding-ada-002`. You need a key from [platform.openai.com](https://platform.openai.com/). There is no way to run the ingestion or search paths without it — the application throws on startup if the key is missing. |

You do not need to install PostgreSQL or pgvector manually. The container images handle that.

---

## Clone the repository

```powershell
git clone <repo-url>
cd "Business Inteligence Layer"
```

---

## Running with Aspire (recommended for development)

This is the easiest path. Aspire starts the database container, injects the connection string, runs migrations, and opens a browser dashboard showing live telemetry.

### Step 1 — Set your OpenAI key

Aspire reads application configuration from the normal .NET configuration stack. The simplest approach during development is to use user secrets so the key is never committed to source control:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/Orchestrator.Api
```

Alternatively you can set it as an environment variable before running:

```powershell
$env:OpenAI__ApiKey = "sk-..."
```

Note the double underscore (`__`) when using environment variables — that is how .NET maps flat env vars to nested configuration keys.

### Step 2 — Start the stack

```powershell
dotnet run --project src/Orchestrator.AppHost
```

What happens:
1. Aspire pulls and starts `pgvector/pgvector:pg16` as a persistent container named `postgres`.
2. Aspire starts the API project and injects the connection string automatically.
3. The API runs EF Core migrations on startup, creating the schema and enabling the `vector` extension.
4. The Aspire dashboard opens at `https://localhost:17114` (or similar — the exact port is printed in the terminal). It shows structured logs, traces, and metrics for both the database and the API.
5. The API is available at the URL printed in the terminal for the `api` resource. It is typically `http://localhost:<random-port>`.

The database container uses `ContainerLifetime.Persistent`, meaning it keeps running between restarts. Your data survives a `dotnet run` restart.

### Step 3 — Seed the demonstration data

The seed endpoint is only available when `ASPNETCORE_ENVIRONMENT=Development`, which is the default when running locally.

Find the API port from the Aspire dashboard, then call:

```powershell
$apiBase = "http://localhost:<port>"   # replace with actual port from Aspire dashboard

Invoke-RestMethod -Uri "$apiBase/api/dev/seed" -Method Post
```

Or with curl:

```bash
curl -s -X POST http://localhost:<port>/api/dev/seed | jq
```

Expected response:

```json
{
  "tenantsCreated": 2,
  "projectsCreated": 6,
  "requirementsCreated": 28,
  "contextChunksIngested": 30
}
```

If the data already exists, the counts will be zero (the operation is idempotent). The embedding ingestion calls OpenAI 30 times (15 chunks per tenant), so it takes a few seconds and uses a small amount of API credit.

---

## Running with docker-compose (CI or no Aspire)

Use this path when you want a self-contained stack without the Aspire developer tooling, or in CI.

### Step 1 — Create the environment file

```powershell
Copy-Item .env.template .env
```

Open `.env` and fill in the two values:

```
DATABASE_PASSWORD=choose-a-strong-password
OPENAI_API_KEY=sk-...
```

### Step 2 — Build and start

```powershell
docker compose up --build
```

The `app` service waits for the `db` service to pass its healthcheck before starting, so ordering is handled automatically.

The API listens on `http://localhost:8080`.  
OpenAPI schema: `http://localhost:8080/openapi/v1.json`

### Step 3 — Seed

```powershell
Invoke-RestMethod -Uri "http://localhost:8080/api/dev/seed" -Method Post
```

Or:

```bash
curl -s -X POST http://localhost:8080/api/dev/seed | jq
```

---

## Running tests

### With Docker (full integration tests)

The test runner script starts Docker Desktop automatically if it is not already running:

```powershell
.\test.ps1
```

What it does:
1. Checks whether Docker Desktop is running. If not, launches it and waits up to 120 seconds for the daemon to become ready.
2. Runs `dotnet test Orchestrator.slnx` with normal verbosity.

Testcontainers pulls `pgvector/pgvector:pg16` (cached after first pull), starts a fresh container for each test class, runs migrations, executes the tests, and tears the container down. You do not need to manage the container lifecycle.

### Without Docker (unit tests only)

```powershell
dotnet test Orchestrator.slnx
```

Integration tests detect that Docker is unavailable and skip themselves gracefully. They will report as `Skipped` rather than `Failed`.

### Passing extra arguments

`test.ps1` forwards any trailing arguments to `dotnet test`:

```powershell
# Run only tests in a specific class
.\test.ps1 --filter "FullyQualifiedName~BusinessContextTests"

# Generate a TRX report
.\test.ps1 --logger "trx;LogFileName=results.trx"
```

---

## First API calls

These examples assume the Aspire path with the API at `http://localhost:<port>`. Substitute `http://localhost:8080` for docker-compose.

The two tenant IDs seeded by `/api/dev/seed`:

| Company | Tenant ID |
|---|---|
| FibreCore Networks | `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1` |
| SwiftFibre | `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2` |

### Search FibreCore's knowledge base

```bash
curl -s \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  "http://localhost:<port>/api/business-context/search?query=how+do+we+respond+to+a+competitor+gaining+market+share&topK=3" \
  | jq
```

You should get back FibreCore's competitive response policy chunks — the deliberate, committee-escalation-style response.

### Search SwiftFibre's knowledge base with the same query

```bash
curl -s \
  -H "X-Tenant-Id: b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2" \
  "http://localhost:<port>/api/business-context/search?query=how+do+we+respond+to+a+competitor+gaining+market+share&topK=3" \
  | jq
```

You get SwiftFibre's brand positioning and acquisition strategy instead — completely different content from a completely different internal perspective, despite the identical query text. This is the tenant isolation working correctly.

### Ingest a new context chunk

```bash
curl -s -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  -d '{"text": "Our legal team requires 30 days notice before any public statement about pricing changes.", "source": "Legal Policy v1.0", "category": "regulatory_compliance"}' \
  "http://localhost:<port>/api/business-context" \
  | jq
```

The response includes the new record's ID. That chunk is now searchable for FibreCore and invisible to SwiftFibre.

### Check health

```bash
curl -s http://localhost:<port>/health
curl -s http://localhost:<port>/alive
```

Both return `Healthy` when the database connection is up and migrations have run.

---

## Building the solution without running it

```powershell
dotnet build Orchestrator.slnx
```

This compiles all seven projects. You do not need Docker or an OpenAI key to build.

---

## Adding an EF Core migration

If you modify any entity class in `Orchestrator.Core`, create a migration before the next startup:

```powershell
dotnet ef migrations add <DescriptiveName> `
  --project src/Orchestrator.Infrastructure `
  --startup-project src/Orchestrator.Api
```

`AppDbContextFactory` in the Infrastructure project provides the design-time connection so this command works without a running database.
