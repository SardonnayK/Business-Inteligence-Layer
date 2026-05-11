# system-context.md
# AI Agent Reference — Business Intelligence Layer Orchestrator

---

## SYSTEM IDENTITY

Multi-tenant hybrid RAG API for business context storage and retrieval. Stores tenant-scoped text chunks with AI-generated vector embeddings in PostgreSQL/pgvector. Exposes HTTP endpoints for ingestion, semantic search, department management, and provider configuration. An AI supervisor routes each ingested chunk to the correct department artifact. Built on ASP.NET Core + Entity Framework Core + .NET Aspire. Embedding-provider agnostic: supports OpenAI, Azure OpenAI, and Ollama via a DB-backed `EmbeddingProviderConfig`.

---

## OPERATIONAL STATE

### WORKING (verified in code)
- `POST /api/business-context` — ingest text; AI supervisor routes to artifact; embedding stored
- `GET /api/business-context/search` — hybrid vector+relational search with optional artifact/department scope
- `POST /api/dev/seed` — idempotent seed of both tenants (Development env only)
- `GET /api/departments`, `POST /api/tenants/{id}/discover-departments` — department management + AI discovery
- `GET/PUT/DELETE /api/tenants/{id}/department-manifest` — per-tenant supervisor guidance blob
- `GET /api/artifacts`, `GET /api/artifacts/{id}/contexts`, `DELETE /api/artifacts/{id}/contexts` — artifact management
- `POST/DELETE /api/artifacts/{id}/departments/{departmentId}` — artifact-to-department membership management
- `PUT/GET /api/embedding-config/system` — system-wide default provider config
- `PUT/GET/DELETE /api/embedding-config/tenant/{id}` — per-tenant provider config override
- `GET /api/embedding-config/providers` — list of provider types with metadata
- `ArtifactSupervisorService` — routes text to artifacts via LLM; falls back gracefully when no ChatModelId
- `EmbeddingProviderFactory` — creates/caches `IEmbeddingGenerator` instances from DB config
- `ChatProviderFactory` — creates/caches `IChatClient` instances from DB config; returns null when ChatModelId absent
- `GuardrailMiddleware` — response buffering + tenant validation for `/api/generate/*`
- `GuardrailService.ValidateAsync` — tenant existence + `IsActive` check
- Auto-migration on startup via `db.Database.MigrateAsync()`
- OpenTelemetry traces/metrics/logs via `AddServiceDefaults()`
- Health endpoints: `GET /health`, `GET /alive`
- OpenAPI spec: `GET /openapi/v1.json` (Development only)
- CORS: `dashboard` policy allows `localhost:5173` and `localhost:3000`

### STUBBED / NOT IMPLEMENTED
- `GuardrailService` policy evaluation — comment reads "Extension point: load policy-as-code rules"; returns `IsValid=true` for any active tenant regardless of content
- No `/api/generate/*` controllers exist — guardrail middleware wired but nothing to intercept
- `BusinessContextPlugin` (Semantic Kernel) defined in `Orchestrator.Engine` but not registered in DI and not used
- No authentication/authorization on any endpoint
- No vector index (IVFFlat / HNSW) — sequential scans at large scale

---

## HTTP ENDPOINTS

### PUT /api/embedding-config/system
**Purpose:** Create or update system-wide default provider config.
**Auth:** None
**Content-Type:** `application/json`

Request body:
```json
{
  "providerType": "OpenAI | AzureOpenAI | Ollama",
  "modelId": "<embedding model name>",
  "apiKey": "<string | null>",
  "endpoint": "<string | null>",
  "chatModelId": "<string | null>"
}
```

Response `200 OK`:
```json
{
  "id": "uuid",
  "tenantId": null,
  "providerType": "string",
  "modelId": "string",
  "endpoint": "string | null",
  "hasApiKey": true,
  "chatModelId": "string | null",
  "updatedAt": "ISO 8601"
}
```

Note: `apiKey` is NEVER returned. `hasApiKey` is a boolean flag.

---

### GET /api/embedding-config/system
**Purpose:** Retrieve system-wide default config.
**Response:** Same shape as PUT response, or `404` if none configured.

---

### PUT /api/embedding-config/tenant/{tenantId}
**Purpose:** Create or update a per-tenant provider config override.
**Response:** Same shape as system PUT response but with `tenantId` populated.

---

### GET /api/embedding-config/tenant/{tenantId}
**Purpose:** Retrieve a tenant-specific config. Returns `404` if none exists (does NOT fall back to system default).

---

### DELETE /api/embedding-config/tenant/{tenantId}
**Purpose:** Remove tenant-specific config. After deletion the tenant uses the system default.
**Response:** `204 No Content`

---

### GET /api/embedding-config/providers
**Purpose:** List provider types with field requirements for UI.
**Response:**
```json
[
  { "value": "None",        "label": "Not configured",  "requiresApiKey": false, "requiresEndpoint": false },
  { "value": "OpenAI",      "label": "OpenAI",          "requiresApiKey": true,  "requiresEndpoint": false },
  { "value": "AzureOpenAI", "label": "Azure OpenAI",    "requiresApiKey": true,  "requiresEndpoint": true  },
  { "value": "Ollama",      "label": "Ollama (local)",  "requiresApiKey": false, "requiresEndpoint": false }
]
```

---

### POST /api/business-context
**Purpose:** Ingest one text chunk. AI supervisor routes to artifact before embedding.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`
**Content-Type:** `application/json`

Request body:
```json
{
  "text":     "string (required)",
  "source":   "string | null",
  "category": "string | null"
}
```

Response `200 OK`:
```json
{
  "id":           "uuid",
  "tenantId":     "uuid",
  "artifactId":   "uuid | null",
  "artifactName": "string | null",
  "departments":  [{ "id": "uuid", "name": "string" }],
  "isShared":     true | false,
  "createdAt":    "ISO 8601 UTC"
}
```

Side effects:
- Calls `ArtifactSupervisorService.RouteToArtifactAsync` (may call LLM if `ChatModelId` configured)
- Calls `IEmbeddingGenerator.GenerateAsync([text])` via configured provider
- Inserts one row into `BusinessContexts` with vector embedding
- May create new Department + Artifact rows if supervisor decides content belongs in a new department

Error conditions:
- `400` — malformed body or missing `text`
- `500` — no embedding provider configured (no system default AND no tenant config)

---

### GET /api/business-context/search
**Purpose:** Hybrid semantic search scoped to one tenant.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`

Query parameters:
| Param | Type | Default | Required |
|-------|------|---------|----------|
| query | string | — | YES |
| topK  | int  | 5 | NO |
| artifactId | guid | — | NO |
| departmentId | guid | — | NO |

Scope resolution: `artifactId` > `departmentId` > tenant-wide.

Response `200 OK` — array:
```json
[
  {
    "id":         "uuid",
    "text":       "string",
    "source":     "string | null",
    "category":   "string | null",
    "artifactId": "uuid | null",
    "artifactName": "string | null",
    "createdAt":  "ISO 8601 UTC"
  }
]
```

Query internals:
```sql
WHERE TenantId = @tenantId
  [AND ArtifactId = @artifactId]
  [AND EXISTS (
      SELECT 1 FROM ArtifactDepartments ad
      WHERE ad.ArtifactId = BusinessContexts.ArtifactId
        AND ad.DepartmentId = @departmentId
  )]
ORDER BY Embedding <-> @queryVector
LIMIT topK
```
Distance metric: L2 (Euclidean) via pgvector `<->` operator.

---

### GET /api/departments
**Purpose:** List all departments for a tenant with artifact count.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`

Response `200 OK`:
```json
[
  {
    "id":            "uuid",
    "name":          "string",
    "description":   "string",
    "estimatedSize": "small | medium | large",
    "createdAt":     "ISO 8601",
    "artifactCount": 2
  }
]
```

---

### POST /api/tenants/{id}/discover-departments
**Purpose:** AI-assisted department and artifact discovery from existing knowledge chunks.
**Auth:** None
**Body:** none

Behavior:
1. Samples up to 30 existing `BusinessContext` chunks for the tenant.
2. If `ChatModelId` configured: sends to LLM for structured department identification.
3. If no `ChatModelId`: creates a fallback `"General"` department with one artifact.
4. Upserts Departments and Artifacts (skips existing by name — idempotent).

Response `200 OK`:
```json
{
  "wasAiAssisted": true,
  "departments": [...],
  "artifacts": [...]
}
```

---

### GET /api/artifacts
**Purpose:** List all artifacts for a tenant including shared artifact.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`

Response `200 OK`:
```json
[
  {
    "id":          "uuid",
    "name":        "string",
    "description": "string",
    "isShared":    true | false,
    "departments": [{ "id": "uuid", "name": "string" }],
    "chunkCount":  12,
    "createdAt":   "ISO 8601"
  }
]
```

---

### GET /api/artifacts/{id}/contexts
**Purpose:** List all BusinessContext chunks in an artifact.
**Auth:** None

Response `200 OK`: array of `{ id, text, source, category, createdAt }`.

---

### DELETE /api/artifacts/{id}/contexts
**Purpose:** Clear all chunks from an artifact (for re-ingestion). Artifact is not deleted.
**Response:** `204 No Content`

---

### POST /api/artifacts/{id}/departments/{departmentId}
**Purpose:** Add a department membership to an artifact (insert into `ArtifactDepartments`). Idempotent.
**Auth:** None
**Body:** none
**Response:** `204 No Content`

---

### DELETE /api/artifacts/{id}/departments/{departmentId}
**Purpose:** Remove a department membership from an artifact. Artifact and department are not deleted.
**Auth:** None
**Response:** `204 No Content`

---

### GET /api/tenants/{id}/department-manifest
**Purpose:** Retrieve the free-text supervisor guidance manifest for a tenant.
**Auth:** None

Response `200 OK`:
```json
{
  "tenantId":  "uuid",
  "content":   "string (Markdown)",
  "updatedAt": "ISO 8601"
}
```
Returns `404` if no manifest has been set.

---

### PUT /api/tenants/{id}/department-manifest
**Purpose:** Create or replace the department manifest for a tenant.
**Auth:** None
**Content-Type:** `application/json`

Request body:
```json
{ "content": "string (required)" }
```

Response `200 OK`: same shape as GET response.

---

### DELETE /api/tenants/{id}/department-manifest
**Purpose:** Remove the department manifest. Supervisor falls back to artifact catalog only.
**Response:** `204 No Content`

---

### POST /api/dev/seed
**Purpose:** Idempotent seed of FibreCore and SwiftFibre demo tenants.
**Auth:** None
**Availability:** Development environment only

Response `200 OK`:
```json
{
  "tenantsCreated":        int,
  "projectsCreated":       int,
  "requirementsCreated":   int,
  "contextChunksIngested": int
}
```

Side effects: inserts entities if absent; calls embedding provider once per un-seeded chunk.

---

### GET /health, GET /alive
Registered via `MapDefaultEndpoints()` from `Orchestrator.ServiceDefaults`. Standard ASP.NET health responses.

---

## AI SUPERVISOR

### RouteToArtifactAsync

Called on every `POST /api/business-context`.

Flow:
1. Load `EmbeddingProviderConfig` for tenant (tenant-specific → system default → throw).
2. Load all `Artifact` rows for tenant.
3. If no artifacts exist: create shared `"Company Knowledge"` artifact and return it (bootstrap).
4. If `ChatModelId` is null/empty: return shared artifact (no LLM available).
5. Otherwise: call LLM with structured JSON prompt listing all artifacts.

LLM prompt returns one of:
```json
{"action":"route","artifactId":"<existing-uuid>"}
{"action":"create","departmentName":"...","artifactName":"...","artifactDescription":"..."}
{"action":"shared"}
```

On `"create"`: creates a new `Department` (size `"small"`) and a new `Artifact`, then creates an `ArtifactDepartment` link between them; returns that artifact.
On `"shared"` or any parse error: returns the tenant's shared artifact.
On any LLM exception: catches, falls back to shared artifact (no crash).

### DiscoverDepartmentsAsync

Called on `POST /api/tenants/{id}/discover-departments`.

Flow:
1. Sample up to 30 `BusinessContext` chunks (random selection using `NEWID()` order).
2. Load `DepartmentManifest` for the tenant (may be null).
3. If no `ChatModelId`: create `"General"` department + 1 artifact; return `WasAiAssisted: false`.
4. Otherwise: send texts (and manifest content if present) to LLM; prompt returns:
```json
{
  "departments": [
    {
      "name": "Engineering",
      "description": "...",
      "estimatedSize": "large",
      "artifacts": [
        {"name":"Technical Standards","description":"..."},
        {"name":"Engineering Processes","description":"..."},
        {"name":"Architecture Decisions","description":"..."}
      ]
    }
  ],
  "sharedArtifact": {"name":"Company Knowledge","description":"..."}
}
```
The number of artifacts per department is determined by the LLM based on content and manifest guidance — there is no hardcoded size-to-count mapping.

5. Upsert each department (skip if name already exists for tenant).
6. Upsert each artifact within that department (skip if name already exists for that department); create `ArtifactDepartment` links.
7. Upsert shared artifact (skip if `IsShared = true` artifact already exists for tenant).

---

## PROVIDER RESOLUTION

At every ingest or search call:
1. Try `EmbeddingProviderConfigs WHERE TenantId = @tenantId` (tenant-specific).
2. Try `EmbeddingProviderConfigs WHERE TenantId IS NULL` (system default).
3. Throw `InvalidOperationException` if neither exists.

Generator creation is cached in `ConcurrentDictionary` keyed on `{ProviderType}|{ModelId}|{Endpoint}|{SHA256(ApiKey)}`. Generators are created once and reused.

Provider implementations:
- **OpenAI:** `new OpenAIClient(apiKey, options).GetEmbeddingClient(modelId).AsIEmbeddingGenerator()` and `.GetChatClient(chatModelId).AsIChatClient()`
- **AzureOpenAI:** Same pattern but with endpoint set on `OpenAIClientOptions`
- **Ollama:** `new OllamaSharp.OllamaApiClient(endpoint, modelId)` — implements both `IEmbeddingGenerator<string, Embedding<float>>` and `IChatClient` directly

---

## ENTITY MODEL

### Table: Tenants
| Column    | Type        | Nullable | Constraints    |
|-----------|-------------|----------|----------------|
| Id        | uuid        | NO       | PK             |
| Name      | text        | NO       | default ''     |
| IsActive  | boolean     | NO       | default true   |
| CreatedAt | timestamptz | NO       | default UtcNow |

### Table: EmbeddingProviderConfigs
| Column       | Type        | Nullable | Constraints                         |
|--------------|-------------|----------|-------------------------------------|
| Id           | uuid        | NO       | PK                                  |
| TenantId     | uuid        | YES      | FK → Tenants; NULL = system default |
| ProviderType | int (enum)  | NO       | None/OpenAI/AzureOpenAI/Ollama      |
| ModelId      | text        | NO       | Embedding model name                |
| ApiKey       | text        | YES      | Never returned via HTTP             |
| Endpoint     | text        | YES      |                                     |
| ChatModelId  | text        | YES      | Supervisor LLM model name           |
| UpdatedAt    | timestamptz | NO       | default UtcNow                      |

### Table: Departments
| Column        | Type        | Nullable | Constraints    |
|---------------|-------------|----------|----------------|
| Id            | uuid        | NO       | PK             |
| TenantId      | uuid        | NO       | FK → Tenants   |
| Name          | text        | NO       | default ''     |
| Description   | text        | NO       | default ''     |
| EstimatedSize | text        | NO       | default ''     |
| CreatedAt     | timestamptz | NO       | default UtcNow |

### Table: Artifacts
| Column      | Type        | Nullable | Constraints    |
|-------------|-------------|----------|----------------|
| Id          | uuid        | NO       | PK             |
| TenantId    | uuid        | NO       | FK → Tenants   |
| Name        | text        | NO       | default ''     |
| Description | text        | NO       | default ''     |
| IsShared    | boolean     | NO       | default false  |
| CreatedAt   | timestamptz | NO       | default UtcNow |
| UpdatedAt   | timestamptz | NO       | default UtcNow |

### Table: ArtifactDepartments
| Column       | Type | Nullable | Constraints                        |
|--------------|------|----------|------------------------------------|
| ArtifactId   | uuid | NO       | PK (composite); FK → Artifacts     |
| DepartmentId | uuid | NO       | PK (composite); FK → Departments   |

### Table: DepartmentManifests
| Column    | Type        | Nullable | Constraints                              |
|-----------|-------------|----------|------------------------------------------|
| Id        | uuid        | NO       | PK                                       |
| TenantId  | uuid        | NO       | FK → Tenants; UNIQUE                     |
| Content   | text        | NO       | Free-text guidance blob (Markdown)       |
| UpdatedAt | timestamptz | NO       | default UtcNow                           |

### Table: BusinessContexts
| Column     | Type        | Nullable | Constraints              |
|------------|-------------|----------|--------------------------|
| Id         | uuid        | NO       | PK                       |
| TenantId   | uuid        | NO       | FK → Tenants; INDEX      |
| ArtifactId | uuid        | YES      | FK → Artifacts; INDEX    |
| Text       | text        | NO       | default ''               |
| Embedding  | vector      | NO       | pgvector; dimensionless  |
| Source     | text        | YES      |                          |
| Category   | text        | YES      |                          |
| CreatedAt  | timestamptz | NO       | default UtcNow           |

Embedding dimension is not fixed at the column level — accepts any provider's output dimension.

### Table: Projects
| Column      | Type        | Nullable | Constraints    |
|-------------|-------------|----------|----------------|
| Id          | uuid        | NO       | PK             |
| TenantId    | uuid        | NO       | FK → Tenants   |
| Name        | text        | NO       | default ''     |
| Description | text        | NO       | default ''     |
| CreatedAt   | timestamptz | NO       | default UtcNow |

### Table: Requirements
| Column    | Type        | Nullable | Constraints    |
|-----------|-------------|----------|----------------|
| Id        | uuid        | NO       | PK             |
| ProjectId | uuid        | NO       | FK → Projects  |
| Content   | text        | NO       | default ''     |
| Status    | text        | NO       | default 'Draft'|
| CreatedAt | timestamptz | NO       | default UtcNow |

---

## SEED DATA INDEX

### FibreCore Networks
- **TenantId:** `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`
- **IsActive:** true

| Project | GUID | Requirement Count |
|---|---|---|
| Urban FTTP Rollout | `a1a1a1a1-0000-0000-0000-000000000010` | 5 |
| Enterprise Fibre Sales Programme | `a1a1a1a1-0000-0000-0000-000000000020` | 4 |
| National Backbone Capacity Upgrade | `a1a1a1a1-0000-0000-0000-000000000030` | 4 |

Context chunks: 15 total — categories: `pricing_strategy`, `target_demographics`, `rollout_phasing`, `sla_commitments`, `competitive_response`, `partnership_approach`, `regulatory_compliance`.

---

### SwiftFibre
- **TenantId:** `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`
- **IsActive:** true

| Project | GUID | Requirement Count |
|---|---|---|
| SuburbanEdge Rollout Programme | `b2b2b2b2-0000-0000-0000-000000000010` | 5 |
| ZeroCap Disruptive Pricing Programme | `b2b2b2b2-0000-0000-0000-000000000020` | 5 |
| AcquisitionBlitz Customer Growth Initiative | `b2b2b2b2-0000-0000-0000-000000000030` | 5 |

Context chunks: 15 total — categories: `pricing_strategy`, `competitive_differentiation`, `rollout_phasing`, `install_time_slas`, `churn_reduction`, `rural_coverage`, `customer_acquisition`, `target_demographics`.

---

## GUARDRAIL MIDDLEWARE

**Route filter:** only activates on paths starting with `/api/generate`  
**Mechanism:** Buffers entire response body into `MemoryStream`, then calls `IGuardrailService.ValidateAsync`

Current validation logic:
1. Parse `X-Tenant-Id` header — if missing or unparseable, bypass validation and pass through
2. Lookup `Tenant` by PK in DB
3. Tenant not found → HTTP 422 `{"error":"Guardrail violation","reason":"Tenant not found"}`
4. `IsActive == false` → HTTP 422 `{"error":"Guardrail violation","reason":"Tenant is inactive"}`
5. Otherwise → pass through (no content inspection implemented)

**Security gap:** Missing or invalid `X-Tenant-Id` bypasses guardrail entirely.

---

## TENANT ISOLATION MODEL

**Mechanism:** Application-layer filtering. `TenantId` from `X-Tenant-Id` header.
- Ingest: stored as column value on `BusinessContexts`
- Search: EF Core `WHERE TenantId = @tenantId` before vector ordering
- No PostgreSQL row-level security
- No JWT/session binding — caller supplies tenant ID freely

**Security limitations:**
1. Any caller can supply any tenant GUID — no ownership verification
2. No API key, token, or session validates tenant ownership
3. Missing header → model binding failure (400)

---

## KNOWN GAPS

| Gap | Location | Detail |
|---|---|---|
| Guardrail content policy | `GuardrailService.ValidateAsync` | Returns `IsValid=true` for any active tenant; no rule evaluation |
| No `/api/generate/*` controllers | Entire codebase | Middleware wired but nothing routes here |
| BusinessContextPlugin unused | `Orchestrator.Engine` | Defined, not registered in DI, not called |
| No vector index | `AppDbContext.OnModelCreating` | No IVFFlat/HNSW; sequential scans at scale |
| No tenant auth | All endpoints | Caller freely sets `X-Tenant-Id` |
| Guardrail header bypass | `GuardrailMiddleware` | Missing/invalid header skips validation |
| `SKEXP0010` suppressed | `Orchestrator.Infrastructure.csproj` | Embedding API experimental in SK 1.75 |
