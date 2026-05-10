# Getting Started

## Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 10 SDK** | Targets `net10.0`. Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download). Verify with `dotnet --version` — you need `10.x.x`. |
| **Docker Desktop** | Required for the pgvector database container. Aspire and the test runner both start containers automatically. |

You do **not** need an OpenAI API key to start. The system is embedding-provider agnostic — you configure an AI provider through the API after the stack is running. Ollama (local, free) and OpenAI are both supported out of the box.

---

## Running with Aspire (recommended for development)

Aspire starts the database container, injects the connection string, runs migrations, and opens a browser dashboard showing live telemetry.

### Step 1 — Start the stack

```powershell
dotnet run --project src/Orchestrator.AppHost
```

What happens:
1. Aspire pulls and starts `pgvector/pgvector:pg16` as a persistent container.
2. Aspire starts the API project and injects the connection string.
3. The API runs EF Core migrations on startup — schema and `vector` extension are created automatically.
4. The Aspire dashboard opens. It shows structured logs, traces, and metrics for the database and API.
5. The API URL is printed next to the `api` resource in the dashboard (typically `http://localhost:<random-port>`).

The database container uses `ContainerLifetime.Persistent`, so data survives between `dotnet run` restarts.

### Step 2 — Configure an embedding provider

The system needs an AI provider before ingestion or search will work. You can use a local Ollama instance (no API key, no cost) or a cloud provider like OpenAI.

**Option A — Ollama (local, free)**

Install [Ollama](https://ollama.com/) and pull an embedding model:

```powershell
ollama pull nomic-embed-text
```

Then configure the system default provider:

```powershell
$api = "http://localhost:<port>"   # replace with your API port from Aspire dashboard

Invoke-RestMethod -Uri "$api/api/embedding-config/system" -Method Put `
  -ContentType "application/json" `
  -Body '{"providerType":"Ollama","modelId":"nomic-embed-text","endpoint":"http://localhost:11434"}'
```

To also enable the AI supervisor (for automatic knowledge routing), add a chat model:

```powershell
ollama pull llama3

Invoke-RestMethod -Uri "$api/api/embedding-config/system" -Method Put `
  -ContentType "application/json" `
  -Body '{"providerType":"Ollama","modelId":"nomic-embed-text","endpoint":"http://localhost:11434","chatModelId":"llama3"}'
```

**Option B — OpenAI**

```powershell
$api = "http://localhost:<port>"

Invoke-RestMethod -Uri "$api/api/embedding-config/system" -Method Put `
  -ContentType "application/json" `
  -Body '{"providerType":"OpenAI","modelId":"text-embedding-ada-002","apiKey":"sk-...","chatModelId":"gpt-4o"}'
```

**Option C — Azure OpenAI**

```powershell
Invoke-RestMethod -Uri "$api/api/embedding-config/system" -Method Put `
  -ContentType "application/json" `
  -Body '{
    "providerType": "AzureOpenAI",
    "modelId": "<your-embedding-deployment-name>",
    "apiKey": "<your-azure-api-key>",
    "endpoint": "https://<your-resource>.openai.azure.com/",
    "chatModelId": "<your-chat-deployment-name>"
  }'
```

### Step 3 — Seed the demonstration data

The seed endpoint is only available in the `Development` environment, which is the default when running locally.

```powershell
Invoke-RestMethod -Uri "$api/api/dev/seed" -Method Post
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

The seeder creates two competing broadband companies (FibreCore Networks and SwiftFibre) with 15 knowledge chunks each. If data already exists the counts are zero — the operation is idempotent.

### Step 4 — Discover departments (optional, requires ChatModelId)

Once chunks are ingested, the AI can analyze the content and automatically organize it into departments and artifacts:

```powershell
$fibreCore = "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"

Invoke-RestMethod -Uri "$api/api/tenants/$fibreCore/discover-departments" -Method Post
```

The response shows which departments and artifacts were created, and whether AI was used (`wasAiAssisted: true/false`).

---

## Running with docker-compose (CI or no Aspire)

Use this path for a self-contained stack without Aspire developer tooling.

### Step 1 — Create the environment file

```powershell
Copy-Item .env.template .env
```

Open `.env` and set the database password:

```
DATABASE_PASSWORD=choose-a-strong-password
```

### Step 2 — Build and start

```powershell
docker compose up --build
```

The `app` service waits for the `db` service to pass its healthcheck before starting.

- API: `http://localhost:8080`
- OpenAPI schema: `http://localhost:8080/openapi/v1.json`

### Step 3 — Configure a provider and seed

```powershell
# Configure Ollama as the system default
Invoke-RestMethod -Uri "http://localhost:8080/api/embedding-config/system" -Method Put `
  -ContentType "application/json" `
  -Body '{"providerType":"Ollama","modelId":"nomic-embed-text","chatModelId":"llama3"}'

# Seed demo data
Invoke-RestMethod -Uri "http://localhost:8080/api/dev/seed" -Method Post
```

---

## Running tests

### With Docker (full integration tests)

The test runner script starts Docker Desktop automatically if it is not already running:

```powershell
.\test.ps1
```

What it does:
1. Checks whether Docker Desktop is running. If not, launches it and waits up to 120 seconds.
2. Runs `dotnet test Orchestrator.slnx`.

Testcontainers pulls `pgvector/pgvector:pg16` (cached after first pull), starts a fresh container per test class, runs migrations, executes the tests, and tears the container down.

### Without Docker (unit tests only)

```powershell
dotnet test Orchestrator.slnx
```

Integration tests detect that Docker is unavailable and skip gracefully — they report as `Skipped` rather than `Failed`.

### Filtering and reports

`test.ps1` forwards any trailing arguments to `dotnet test`:

```powershell
.\test.ps1 --filter "FullyQualifiedName~BusinessContextTests"
.\test.ps1 --logger "trx;LogFileName=results.trx"
```

---

## First API calls

These examples assume the Aspire path. Substitute `http://localhost:8080` for docker-compose.

Seeded tenant IDs:

| Company | Tenant ID |
|---|---|
| FibreCore Networks | `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1` |
| SwiftFibre | `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2` |

### Search FibreCore's knowledge base

```bash
curl -s \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  "http://localhost:<port>/api/business-context/search?query=how+do+we+respond+to+a+competitor+gaining+market+share&topK=3"
```

### Ingest a new chunk (auto-routed by AI supervisor)

```bash
curl -s -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  -d '{"text": "Our legal team requires 30 days notice before any public statement about pricing changes.", "source": "Legal Policy v1.0", "category": "regulatory_compliance"}' \
  "http://localhost:<port>/api/business-context"
```

The response tells you which artifact and department the AI supervisor chose for this chunk.

### List departments for a tenant

```bash
curl -s \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  "http://localhost:<port>/api/departments"
```

### Check provider configuration

```bash
curl -s "http://localhost:<port>/api/embedding-config/system"
```

### Check health

```bash
curl -s http://localhost:<port>/health
curl -s http://localhost:<port>/alive
```

---

## Building without running

```powershell
dotnet build Orchestrator.slnx
```

Compiles all seven projects. No Docker or AI provider key required.

---

## Adding an EF Core migration

If you modify any entity class in `Orchestrator.Core`:

```powershell
dotnet ef migrations add <DescriptiveName> `
  --project src/Orchestrator.Infrastructure `
  --startup-project src/Orchestrator.Api
```

`AppDbContextFactory` in the Infrastructure project provides the design-time connection so this works without a running database.
