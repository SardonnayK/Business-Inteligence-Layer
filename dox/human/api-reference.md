# API Reference

## General conventions

**Base URL (Aspire):** `http://localhost:<port>` — shown in the Aspire dashboard next to the `api` resource.

**Base URL (docker-compose):** `http://localhost:8080`

**OpenAPI schema:** `GET /openapi/v1.json` — only served when `ASPNETCORE_ENVIRONMENT=Development`.

**Tenant identification:** Business-context, department, and artifact endpoints require an `X-Tenant-Id` header containing a GUID. This is the primary isolation boundary.

**Content-Type:** All POST/PUT bodies are `application/json`. All responses are `application/json`.

---

## Embedding provider configuration

### PUT /api/embedding-config/system

Create or replace the system-wide default provider configuration. This applies to all tenants that do not have a tenant-specific override.

**Request body**

```json
{
  "providerType": "OpenAI | AzureOpenAI | Ollama",
  "modelId": "text-embedding-ada-002",
  "apiKey": "sk-... (required for OpenAI and AzureOpenAI; omit for Ollama)",
  "endpoint": "https://... (required for AzureOpenAI; optional for OpenAI/Ollama)",
  "chatModelId": "gpt-4o (optional — enables AI supervisor routing and department discovery)"
}
```

**Example — Ollama (local, no API key)**

```bash
curl -s -X PUT \
  -H "Content-Type: application/json" \
  -d '{"providerType":"Ollama","modelId":"nomic-embed-text","endpoint":"http://localhost:11434","chatModelId":"llama3"}' \
  http://localhost:8080/api/embedding-config/system
```

**Response 200 OK**

```json
{
  "id": "...",
  "tenantId": null,
  "providerType": "Ollama",
  "modelId": "nomic-embed-text",
  "endpoint": "http://localhost:11434",
  "hasApiKey": false,
  "chatModelId": "llama3",
  "updatedAt": "2026-05-10T09:00:00Z"
}
```

`apiKey` is never returned in any response. `hasApiKey` indicates whether a key is stored.

---

### GET /api/embedding-config/system

Returns the current system-wide default config. Returns 404 if none has been configured yet.

---

### PUT /api/embedding-config/tenant/{tenantId}

Create or replace a provider configuration for a specific tenant. Overrides the system default for that tenant.

```bash
curl -s -X PUT \
  -H "Content-Type: application/json" \
  -d '{"providerType":"OpenAI","modelId":"text-embedding-3-large","apiKey":"sk-...","chatModelId":"gpt-4o"}' \
  http://localhost:8080/api/embedding-config/tenant/a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1
```

---

### GET /api/embedding-config/tenant/{tenantId}

Returns the tenant-specific config, or 404 if none exists for that tenant (does not fall back to the system default in this response).

---

### DELETE /api/embedding-config/tenant/{tenantId}

Removes the tenant-specific override. After deletion the tenant uses the system default.

---

### GET /api/embedding-config/providers

Returns the list of supported provider types with metadata about which fields each requires:

```json
[
  { "value": "None",         "label": "Not configured",   "requiresApiKey": false, "requiresEndpoint": false },
  { "value": "OpenAI",       "label": "OpenAI",           "requiresApiKey": true,  "requiresEndpoint": false },
  { "value": "AzureOpenAI",  "label": "Azure OpenAI",     "requiresApiKey": true,  "requiresEndpoint": true  },
  { "value": "Ollama",       "label": "Ollama (local)",   "requiresApiKey": false, "requiresEndpoint": false }
]
```

---

## Business Context endpoints

### POST /api/business-context

Ingest a text chunk. The AI supervisor decides which artifact the text belongs to. The configured embedding model generates a vector, and both are persisted.

**Headers**

| Header | Required | Description |
|---|---|---|
| `X-Tenant-Id` | Yes | The tenant's GUID |
| `Content-Type` | Yes | `application/json` |

**Request body**

```json
{
  "text": "string (required)",
  "source": "string or null",
  "category": "string or null"
}
```

**Example request**

```bash
curl -s -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  -d '{
    "text": "FibreCore does not match competitor pricing. Our response to a price challenge is a retention campaign, not a rate card change.",
    "source": "Competitive Response Playbook v1.1",
    "category": "competitive_response"
  }' \
  http://localhost:8080/api/business-context
```

**Response 200 OK**

```json
{
  "id": "3f7c2a1e-...",
  "tenantId": "a1a1a1a1-...",
  "artifactId": "e5d3c2b1-...",
  "artifactName": "Competitive Strategy",
  "departmentId": "f9a8b7c6-...",
  "departmentName": "Sales",
  "isShared": false,
  "createdAt": "2026-05-10T09:14:22.341Z"
}
```

When `isShared` is `true`, the chunk was routed to the company-wide shared artifact; `departmentId` and `departmentName` will be null.

**Notes**
- The AI supervisor runs before embedding. If no `ChatModelId` is configured, the chunk goes to the shared artifact automatically.
- There is no deduplication. The same text submitted twice creates two rows.

---

### GET /api/business-context/search

Search a tenant's knowledge base. Results are ranked by vector similarity (L2 distance).

**Headers**

| Header | Required |
|---|---|
| `X-Tenant-Id` | Yes |

**Query parameters**

| Parameter | Required | Default | Description |
|---|---|---|---|
| `query` | Yes | — | Natural-language query |
| `topK` | No | `5` | Maximum results |
| `artifactId` | No | — | Narrows search to a single artifact |
| `departmentId` | No | — | Narrows search to all artifacts in a department |

When both `artifactId` and `departmentId` are omitted, the search spans all chunks for the tenant.

**Example — tenant-wide search**

```bash
curl -s \
  -H "X-Tenant-Id: b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2" \
  "http://localhost:8080/api/business-context/search?query=rural+broadband+commitments&topK=2"
```

**Example — scoped to a specific artifact**

```bash
curl -s \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  "http://localhost:8080/api/business-context/search?query=SLA+penalties&artifactId=<artifact-uuid>"
```

**Response 200 OK**

```json
[
  {
    "id": "1a2b3c4d-...",
    "text": "SwiftFibre rural coverage commitment...",
    "source": "SwiftFibre Rural Coverage Commitment — Public Statement",
    "category": "rural_coverage",
    "artifactId": "...",
    "artifactName": "Rural Coverage",
    "createdAt": "2026-05-10T08:00:00Z"
  }
]
```

An empty array is a valid response.

---

## Department endpoints

### GET /api/departments

List all departments for a tenant, with the count of artifacts in each.

**Headers:** `X-Tenant-Id: <guid>`

**Response 200 OK**

```json
[
  {
    "id": "...",
    "name": "Engineering",
    "description": "Technical standards, processes and architecture",
    "estimatedSize": "large",
    "createdAt": "2026-05-10T...",
    "artifactCount": 3
  }
]
```

---

### POST /api/tenants/{id}/discover-departments

Trigger AI-assisted department and artifact discovery. The supervisor samples up to 30 existing chunks, identifies distinct departments, and creates the appropriate artifact structure. Idempotent — departments and artifacts whose names already exist are skipped.

**No request body or headers required.** The tenant ID is in the path.

**Response 200 OK**

```json
{
  "wasAiAssisted": true,
  "departments": [
    { "id": "...", "name": "Sales", "description": "...", "estimatedSize": "small", "createdAt": "..." }
  ],
  "artifacts": [
    { "id": "...", "name": "Sales Playbook", "description": "...", "isShared": false, "departmentId": "...", "tenantId": "...", "createdAt": "..." }
  ]
}
```

When `wasAiAssisted` is `false`, no `ChatModelId` was configured — a fallback `"General"` department with a single artifact was created instead.

---

## Artifact endpoints

### GET /api/artifacts

List all artifacts for a tenant, including the shared artifact. Each entry includes a chunk count and the owning department name.

**Headers:** `X-Tenant-Id: <guid>`

**Response 200 OK**

```json
[
  {
    "id": "...",
    "name": "Company Knowledge",
    "description": "Company-wide goals, mission and cross-cutting policies",
    "isShared": true,
    "departmentId": null,
    "departmentName": null,
    "chunkCount": 4,
    "createdAt": "..."
  },
  {
    "id": "...",
    "name": "Technical Standards",
    "description": "Engineering technical standards and architecture decisions",
    "isShared": false,
    "departmentId": "...",
    "departmentName": "Engineering",
    "chunkCount": 12,
    "createdAt": "..."
  }
]
```

---

### GET /api/artifacts/{id}/contexts

List all `BusinessContext` chunks stored in a specific artifact.

**Response 200 OK**

```json
[
  {
    "id": "...",
    "text": "...",
    "source": "...",
    "category": "...",
    "createdAt": "..."
  }
]
```

---

### DELETE /api/artifacts/{id}/contexts

Clear all chunks from an artifact without deleting the artifact itself. Used to re-ingest updated content into a clean artifact.

**Response 204 No Content**

---

## Development-only endpoints

Available only when `ASPNETCORE_ENVIRONMENT=Development`.

### POST /api/dev/seed

Populates the database with two tenants (FibreCore Networks and SwiftFibre), their projects, requirements, and embedded business context chunks. Idempotent — each entity is checked for existence before insertion.

**No headers or request body required.**

**Response 200 OK — first run**

```json
{
  "tenantsCreated": 2,
  "projectsCreated": 6,
  "requirementsCreated": 28,
  "contextChunksIngested": 30
}
```

**Notes**
- The seeder uses whatever embedding provider is configured at the time of the call. Configure a provider first via `PUT /api/embedding-config/system`.
- Seeded tenant GUIDs are fixed and documented in the data model reference.

---

## Health endpoints

### GET /health

Reports whether the application and its registered health checks are healthy. The database connection is monitored automatically.

```
Healthy
```

### GET /alive

Liveness probe — confirms the process is running. Does not check the database.

---

## Guardrail pipeline

`GuardrailMiddleware` is registered and intercepts requests whose path starts with `/api/generate`. It buffers the response body and validates it against `IGuardrailService`.

**No `/api/generate/*` controllers currently exist.** The middleware is correctly wired and will fire when those endpoints are added.

When they are added, the validation flow is:
1. Middleware buffers the entire response.
2. Reads `X-Tenant-Id` header.
3. Looks up the tenant in the DB.
4. If tenant not found or `IsActive = false` → HTTP 422 `{"error":"Guardrail violation","reason":"..."}`.
5. Otherwise → flushes the buffered response to the client.

---

## Error responses

| Scenario | Status | Body |
|---|---|---|
| Guardrail violation (future `/api/generate/*` routes) | 422 | `{"error":"Guardrail violation","reason":"..."}` |
| No embedding provider configured | 500 | Problem details with message |
| Missing route | 404 | ASP.NET Core problem details |
| Unhandled exception | 500 | ASP.NET Core problem details |

No authentication or authorisation middleware exists. The `X-Tenant-Id` header is not validated against a token or signature — any caller who knows a tenant GUID can access that tenant's data.
