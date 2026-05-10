# system-context.md
# AI Agent Reference — Business Intelligence Layer Orchestrator

---

## SYSTEM IDENTITY

Multi-tenant hybrid RAG API for business context storage and retrieval. Stores tenant-scoped text chunks with OpenAI embeddings in PostgreSQL/pgvector. Exposes HTTP endpoints for ingestion and semantic search. Includes a guardrail middleware layer for AI output validation on `/api/generate/*` routes. Built on ASP.NET Core + Entity Framework Core + .NET Aspire.

---

## OPERATIONAL STATE

### WORKING (verified in code)
- `POST /api/business-context` — ingest text, generate embedding, persist to DB
- `GET /api/business-context/search` — hybrid vector+relational search
- `POST /api/dev/seed` — idempotent seed of both tenants (Development env only)
- `GuardrailMiddleware` — response buffering + tenant validation for `/api/generate/*`
- `GuardrailService.ValidateAsync` — tenant existence + `IsActive` check
- Auto-migration on startup via `db.Database.MigrateAsync()`
- OpenTelemetry traces/metrics/logs via `AddServiceDefaults()`
- Health endpoints: `GET /health`, `GET /alive`
- OpenAPI spec: `GET /openapi/v1.json` (Development only)

### STUBBED / NOT IMPLEMENTED
- `GuardrailService` policy evaluation: comment reads "Extension point: load policy-as-code rules from relational DB and evaluate" — currently returns `IsValid=true` for any active tenant regardless of content
- No `/api/generate/*` controllers exist — guardrail middleware is wired but has nothing to intercept
- `BusinessContextPlugin` (Semantic Kernel) is defined but not registered in DI and not used by any controller
- No authentication/authorization on any endpoint (no `[Authorize]`, no API key check)
- No CRUD for Tenant, Project, or Requirement entities via HTTP
- No pagination on search results beyond `topK`

---

## HTTP ENDPOINTS

### POST /api/business-context
**Purpose:** Ingest one text chunk; generates and stores embedding.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`
**Content-Type:** `application/json`

Request body:
```
{
  "text":     string (required, non-null),
  "source":   string | null,
  "category": string | null
}
```

Response `200 OK`:
```
{
  "id":        string (uuid),
  "tenantId":  string (uuid),
  "createdAt": string (ISO 8601 UTC datetime)
}
```

Side effects:
- Calls OpenAI `text-embedding-ada-002` API (network, latency, cost)
- Inserts one row into `BusinessContexts` table
- Embedding stored as `vector(1536)` in pgvector column

Error conditions:
- `400` if body is malformed or `text` is missing (model binding failure)
- No explicit `404` for unknown tenant — tenant is not validated on ingest

---

### GET /api/business-context/search
**Purpose:** Hybrid semantic search scoped to one tenant.
**Auth:** None
**Required header:** `X-Tenant-Id: <guid>`

Query parameters:
| Param | Type | Default | Required |
|-------|------|---------|----------|
| query | string | — | YES |
| topK  | int  | 5       | NO  |

Response `200 OK` — array:
```
[
  {
    "id":        string (uuid),
    "text":      string,
    "source":    string | null,
    "category":  string | null,
    "createdAt": string (ISO 8601 UTC datetime)
  }
]
```

Side effects:
- Calls OpenAI `text-embedding-ada-002` to embed the query (network, latency, cost)
- Read-only DB query

Query internals: `WHERE TenantId = @tenantId ORDER BY embedding <-> @queryVector LIMIT topK`
Distance metric: L2 (Euclidean) via pgvector `<->` operator

Note: `Embedding` field is NOT returned in search response (stripped in controller projection).

---

### POST /api/dev/seed
**Purpose:** Idempotent seed of both FibreCore and SwiftFibre tenants.
**Auth:** None
**Availability:** Development environment only (`app.Environment.IsDevelopment()`)
**Body:** none

Response `200 OK`:
```
{
  "tenantsCreated":       int,
  "projectsCreated":      int,
  "requirementsCreated":  int,
  "contextChunksIngested": int
}
```

Side effects:
- Creates tenants, projects, requirements if not already present (checked by GUID)
- Calls OpenAI embeddings API once per context chunk not yet ingested (15 calls per tenant = up to 30 total)
- Idempotency: skips tenant/project/requirement if GUID already exists; skips chunk ingestion if `existingCount >= chunks.Length`

---

### GET /health, GET /alive
Registered via `MapDefaultEndpoints()` from `Orchestrator.ServiceDefaults`. Standard ASP.NET health check responses.

---

### GET /openapi/v1.json
Development only. OpenAPI v1 spec. No auth required.

---

## GUARDRAIL MIDDLEWARE

**Route filter:** only activates on paths starting with `/api/generate`
**Mechanism:** Buffers entire response body into `MemoryStream`, then calls `IGuardrailService.ValidateAsync`

Validation logic (current implementation):
1. Parse `X-Tenant-Id` header as GUID — if missing or unparseable, **bypass validation and pass through**
2. Lookup `Tenant` by PK in DB
3. If tenant not found → HTTP 422, `{ "error": "Guardrail violation", "reason": "Tenant not found" }`
4. If `tenant.IsActive == false` → HTTP 422, `{ "error": "Guardrail violation", "reason": "Tenant is inactive" }`
5. Otherwise → pass through (no content inspection implemented)

**Security gap:** Missing or invalid `X-Tenant-Id` bypasses guardrail entirely (lines 32-33 in GuardrailMiddleware.cs — validation block is skipped).

---

## TENANT ISOLATION MODEL

**Mechanism:** Application-layer filtering only. `TenantId` (Guid) passed as HTTP header `X-Tenant-Id`.

How it works:
- Ingest: `TenantId` from header is stored as a column on `BusinessContexts` row
- Search: EF Core `WHERE TenantId = @tenantId` applied before vector ordering
- No row-level security in PostgreSQL
- No JWT/session binding of tenant ID — caller supplies it freely

**Security limitations (explicit):**
1. Any caller can supply any tenant GUID — no verification that caller owns that tenant
2. A caller can read another tenant's data by supplying that tenant's GUID in the header
3. No API key, token, or session validates tenant ownership
4. Tenant isolation is entirely trust-based on the caller honoring the header value
5. Missing header on ingest stores data but may cause unexpected behavior — controller parameter `[FromHeader]` is required type `Guid`, so missing header causes model binding failure (400)

---

## RAG PIPELINE

### Ingest flow
1. Receive `text`, `tenantId`, optional `source`, `category`
2. Call `IEmbeddingGenerator<string, Embedding<float>>.GenerateAsync([text])`
   - Provider: OpenAI `text-embedding-ada-002`
   - Output: `float[]` of length 1536
3. Construct `new Vector(float[])` (Pgvector type)
4. Insert `BusinessContext` row with all fields + embedding
5. Return persisted entity

### Search flow
1. Receive `query` string, `tenantId`, `topK`
2. Call `IEmbeddingGenerator<string, Embedding<float>>.GenerateAsync([query])`
   - Same model: OpenAI `text-embedding-ada-002`
   - Output: `float[]` of length 1536
3. Construct `new Vector(float[])`
4. EF Core query:
   ```csharp
   _db.BusinessContexts
       .Where(bc => bc.TenantId == tenantId)
       .OrderBy(bc => bc.Embedding.L2Distance(queryVector))
       .Take(topK)
       .ToListAsync()
   ```
5. Return `IReadOnlyList<BusinessContext>`

**Model:** `text-embedding-ada-002`
**Dimensions:** 1536
**Distance metric:** L2 (Euclidean) — pgvector `<->` operator
**No hybrid re-ranking:** pure vector distance, no BM25 or keyword score blending
**No index defined:** `AppDbContext` only defines `HasIndex(bc => bc.TenantId)` — no IVFFlat or HNSW vector index is configured; at scale, this causes sequential scans

---

## ENTITY MODEL

### Table: Tenants
| Column    | Type     | Nullable | Constraints       |
|-----------|----------|----------|-------------------|
| Id        | uuid     | NO       | PK                |
| Name      | text     | NO       | default ''        |
| IsActive  | boolean  | NO       | default true      |
| CreatedAt | timestamptz | NO    | default UtcNow    |

Relationships:
- 1:N → Projects (FK: Projects.TenantId)
- 1:N → BusinessContexts (FK: BusinessContexts.TenantId)

### Table: Projects
| Column      | Type     | Nullable | Constraints    |
|-------------|----------|----------|----------------|
| Id          | uuid     | NO       | PK             |
| TenantId    | uuid     | NO       | FK → Tenants   |
| Name        | text     | NO       | default ''     |
| Description | text     | NO       | default ''     |
| CreatedAt   | timestamptz | NO    | default UtcNow |

Relationships:
- N:1 → Tenants
- 1:N → Requirements (FK: Requirements.ProjectId)

### Table: Requirements
| Column    | Type     | Nullable | Constraints    |
|-----------|----------|----------|----------------|
| Id        | uuid     | NO       | PK             |
| ProjectId | uuid     | NO       | FK → Projects  |
| Content   | text     | NO       | default ''     |
| Status    | text     | NO       | default 'Draft' |
| CreatedAt | timestamptz | NO    | default UtcNow |

Status values in seed data: `"Approved"`, `"In Review"`, `"Draft"` (no DB-level enum constraint)

### Table: BusinessContexts
| Column    | Type          | Nullable | Constraints               |
|-----------|---------------|----------|---------------------------|
| Id        | uuid          | NO       | PK                        |
| TenantId  | uuid          | NO       | FK → Tenants; INDEX       |
| Text      | text          | NO       | default ''                |
| Embedding | vector(1536)  | NO       | pgvector column           |
| Source    | text          | YES      |                           |
| Category  | text          | YES      |                           |
| CreatedAt | timestamptz   | NO       | default UtcNow            |

PostgreSQL extension required: `vector` (installed via `modelBuilder.HasPostgresExtension("vector")`)

---

## SEED DATA INDEX

### FibreCore Networks
- **TenantId:** `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`
- **IsActive:** true

| Project Name | Project GUID | Requirement Count | Req Statuses |
|---|---|---|---|
| Urban FTTP Rollout | `a1a1a1a1-0000-0000-0000-000000000010` | 5 | 3 Approved, 1 In Review, 1 Draft |
| Enterprise Fibre Sales Programme | `a1a1a1a1-0000-0000-0000-000000000020` | 4 | 3 Approved, 1 In Review |
| National Backbone Capacity Upgrade | `a1a1a1a1-0000-0000-0000-000000000030` | 4 | 2 Approved, 1 In Review, 1 Draft |

**Total requirements:** 13

Context chunks — 15 total:

| Category | Count | Sources |
|---|---|---|
| pricing_strategy | 2 | Residential Pricing Policy v3.2; Enterprise Commercial Policy v2.1 |
| target_demographics | 2 | Market Segmentation Report 2025; Enterprise Sales Strategy 2025–2027 |
| rollout_phasing | 3 | National FTTP Rollout Master Plan v1.4; Urban Deployment Prioritisation Framework; Communications and Disclosure Policy v4.0 |
| sla_commitments | 2 | Residential SLA Standard Terms; Enterprise SLA Schedule Platinum Tier v2.3 |
| competitive_response | 2 | Competitive Response Playbook v1.1 (two chunks from same source) |
| partnership_approach | 1 | Partner and Channel Policy v3.0 |
| regulatory_compliance | 3 | Regulatory Affairs Policy and Governance Framework; Network Security and Compliance Policy v2.2; Data Governance and Privacy Policy v1.9 |

---

### SwiftFibre
- **TenantId:** `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`
- **IsActive:** true

| Project Name | Project GUID | Requirement Count | Req Statuses |
|---|---|---|---|
| SuburbanEdge Rollout Programme | `b2b2b2b2-0000-0000-0000-000000000010` | 5 | 3 Approved, 1 In Review, 1 Draft |
| ZeroCap Disruptive Pricing Programme | `b2b2b2b2-0000-0000-0000-000000000020` | 5 | 3 Approved, 1 In Review, 1 Draft |
| AcquisitionBlitz Customer Growth Initiative | `b2b2b2b2-0000-0000-0000-000000000030` | 5 | 3 Approved, 1 In Review, 1 Draft |

**Total requirements:** 15

Context chunks — 15 total:

| Category | Count | Sources |
|---|---|---|
| pricing_strategy | 2 | Internal Strategy Brief v2.4 (two chunks) |
| competitive_differentiation | 2 | Brand & Positioning Playbook (two chunks) |
| rollout_phasing | 2 | Operations Handbook — Rollout Standards (two chunks) |
| install_time_slas | 2 | SLA Commitment Charter (two chunks) |
| churn_reduction | 2 | Customer Experience Strategy 2025 (two chunks) |
| rural_coverage | 2 | Rural Coverage Commitment — Public Statement (two chunks) |
| customer_acquisition | 2 | AcquisitionBlitz Campaign Brief (two chunks) |
| target_demographics | 1 | Target Demographics Research — Q1 2025 |

---

## KNOWN GAPS AND STUBS

| Gap | Location | Detail |
|---|---|---|
| Guardrail content policy | `GuardrailService.ValidateAsync` | Returns `IsValid=true` for any active tenant; no rule evaluation |
| No `/api/generate/*` controllers | Entire codebase | Middleware exists but nothing routes here |
| BusinessContextPlugin unused | `Orchestrator.Engine` | Defined, not registered in DI, not called anywhere |
| No vector index | `AppDbContext.OnModelCreating` | No IVFFlat/HNSW index; sequential scans at scale |
| No tenant auth | All endpoints | Caller freely sets `X-Tenant-Id`; no ownership verification |
| Guardrail header bypass | `GuardrailMiddleware` lines 32-33 | Missing/invalid header skips validation entirely |
| No tenant/project/requirement HTTP API | All controllers | Only `BusinessContext` has endpoints |
| OpenAPI in Development only | `Program.cs` line 29 | `app.MapOpenApi()` inside `IsDevelopment()` block |
| `SKEXP0010` suppressed | `Orchestrator.Infrastructure.csproj` | Embedding API is experimental in SK 1.75 |
