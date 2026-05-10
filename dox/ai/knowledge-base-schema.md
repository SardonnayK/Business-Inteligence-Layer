# knowledge-base-schema.md
# AI Agent Reference — Knowledge Base Schema & Operations

---

## Knowledge hierarchy

```
Tenant
├── Artifact [IsShared=true]        ← always present; no department owner
│   └── BusinessContext chunks
└── Department
    └── Artifact [IsShared=false]   ← 1–3 per department
        └── BusinessContext chunks
```

Every `BusinessContext` chunk belongs to exactly one `Artifact`. Every `Artifact` belongs to exactly one `Tenant`, and optionally one `Department`.

---

## BusinessContext — Full Schema

Table: `BusinessContexts`

| Field      | C# Type  | DB Type     | Nullable | Notes                               |
|------------|----------|-------------|----------|-------------------------------------|
| Id         | Guid     | uuid        | NO       | PK; assigned by service (NewGuid)   |
| TenantId   | Guid     | uuid        | NO       | FK → Tenants.Id; indexed            |
| ArtifactId | Guid?    | uuid        | YES      | FK → Artifacts.Id; indexed; set by supervisor |
| Text       | string   | text        | NO       | Raw content chunk                   |
| Embedding  | Vector   | vector      | NO       | pgvector; dimensionless; never returned via HTTP |
| Source     | string?  | text        | YES      | Free-form provenance string         |
| Category   | string?  | text        | YES      | Free-form classification label      |
| CreatedAt  | DateTime | timestamptz | NO       | UTC; set at ingest time             |

**Embedding:** Dimensionless `vector` column — no fixed dimension constraint. Accepts OpenAI (1536), Ollama `nomic-embed-text` (768), or any other provider dimension.  
**Distance metric at query time:** L2 (Euclidean) — pgvector `<->` operator  
**No vector index configured** — full sequential scan; `TenantId` B-tree index applied as relational pre-filter

---

## Artifact — Schema

Table: `Artifacts`

| Field        | C# Type  | DB Type     | Nullable | Notes                         |
|--------------|----------|-------------|----------|-------------------------------|
| Id           | Guid     | uuid        | NO       | PK                            |
| TenantId     | Guid     | uuid        | NO       | FK → Tenants.Id               |
| DepartmentId | Guid?    | uuid        | YES      | FK → Departments.Id; null = shared |
| Name         | string   | text        | NO       | Document/scope title          |
| Description  | string   | text        | NO       | What knowledge this contains  |
| IsShared     | bool     | boolean     | NO       | true = cross-cutting artifact |
| CreatedAt    | DateTime | timestamptz | NO       |                               |
| UpdatedAt    | DateTime | timestamptz | NO       |                               |

**Shared artifact invariant:** Every tenant has exactly one artifact where `IsShared = true` and `DepartmentId = null`. Created automatically on first ingest if no artifacts exist.

---

## Department — Schema

Table: `Departments`

| Field         | C# Type  | DB Type     | Nullable | Notes                              |
|---------------|----------|-------------|----------|------------------------------------|
| Id            | Guid     | uuid        | NO       | PK                                 |
| TenantId      | Guid     | uuid        | NO       | FK → Tenants.Id                    |
| Name          | string   | text        | NO       | e.g. "Engineering", "HR"           |
| Description   | string   | text        | NO       | What this department's knowledge covers |
| EstimatedSize | string   | text        | NO       | "small" | "medium" | "large"      |
| CreatedAt     | DateTime | timestamptz | NO       |                                    |

Size → artifact count mapping:
- `"small"` → 1 artifact
- `"medium"` → 2 artifacts
- `"large"` → 3 artifacts

---

## HOW TO INGEST

**Endpoint:** `POST /api/business-context`  
**Content-Type:** `application/json`

Required header:
```
X-Tenant-Id: <uuid>
```

Request body:
```json
{
  "text":     "<string, required>",
  "source":   "<string | null>",
  "category": "<string | null>"
}
```

Constraints:
- `text` must be non-null (null → 400)
- `tenantId` must be a valid GUID (invalid format → 400)
- Tenant existence is NOT validated on ingest — unknown GUIDs are accepted
- No deduplication — same text submitted twice creates two rows

Successful response `200 OK`:
```json
{
  "id":             "<uuid>",
  "tenantId":       "<uuid>",
  "artifactId":     "<uuid | null>",
  "artifactName":   "<string | null>",
  "departmentId":   "<uuid | null>",
  "departmentName": "<string | null>",
  "isShared":       true | false,
  "createdAt":      "<ISO 8601 UTC>"
}
```

Side effects per call:
1. `ArtifactSupervisorService.RouteToArtifactAsync` — may call LLM (1 chat completion call) if `ChatModelId` configured
2. `IEmbeddingGenerator.GenerateAsync([text])` — 1 embedding call via configured provider
3. One INSERT into `BusinessContexts`
4. Potentially one INSERT into `Departments` + one INSERT into `Artifacts` if supervisor creates a new department

---

## HOW TO SEARCH

**Endpoint:** `GET /api/business-context/search`

Required header:
```
X-Tenant-Id: <uuid>
```

Query parameters:
| Parameter    | Type   | Required | Default | Notes                              |
|--------------|--------|----------|---------|------------------------------------|
| query        | string | YES      | —       | Natural-language phrase            |
| topK         | int    | NO       | 5       | No server-side maximum enforced    |
| artifactId   | guid   | NO       | —       | Narrows to single artifact         |
| departmentId | guid   | NO       | —       | Narrows to all artifacts in dept   |

Scope resolution order: `artifactId` takes precedence over `departmentId`; both absent = tenant-wide.

Example:
```
GET /api/business-context/search?query=pricing+strategy&topK=3
X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1
```

Response `200 OK` — ordered array (closest L2 distance first):
```json
[
  {
    "id":           "<uuid>",
    "text":         "<original chunk text>",
    "source":       "<string | null>",
    "category":     "<string | null>",
    "artifactId":   "<uuid | null>",
    "artifactName": "<string | null>",
    "createdAt":    "<ISO 8601 UTC>"
  }
]
```

Result count: `min(topK, matching_rows)`  
Empty result: `[]` (not an error)  
`Embedding` field: never included in response

Side effects per call:
1. `IEmbeddingGenerator.GenerateAsync([query])` — 1 embedding call
2. One read-only DB query

---

## TENANT SCOPING RULES

**Header:** `X-Tenant-Id` (type: Guid)

| Scenario | Ingest result | Search result |
|---|---|---|
| Valid GUID, tenant exists | Stored under that tenant | Returns only that tenant's chunks |
| Valid GUID, tenant does NOT exist | Stored (no FK violation at app layer) | Returns `[]` |
| Missing header | 400 Bad Request | 400 Bad Request |
| Malformed GUID | 400 Bad Request | 400 Bad Request |

**Security note:** No authentication. Any caller that knows a tenant GUID can read or write that tenant's data.

---

## CATEGORY TAXONOMY — SEED DATA

No enum constraint — any string is valid at ingest time.

### FibreCore Networks (`a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`)

| Category | Count | Topics |
|---|---|---|
| `pricing_strategy` | 2 | Residential premium positioning; enterprise rate card + discount policy |
| `target_demographics` | 2 | Residential (dual-income metro); enterprise (50+ employee orgs) |
| `rollout_phasing` | 3 | 4-phase FTTP plan; sub-zone scoring; disclosure policy |
| `sla_commitments` | 2 | Residential 99.9%; Enterprise Platinum 99.95% with credit schedule |
| `competitive_response` | 2 | No reactive price matching; 5pp loss → Executive Committee review |
| `partnership_approach` | 1 | Selective accredited panel |
| `regulatory_compliance` | 3 | Open-access; Critical Infrastructure Protection; Data Protection Act |

FibreCore total: 15 chunks

---

### SwiftFibre (`b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`)

| Category | Count | Topics |
|---|---|---|
| `pricing_strategy` | 2 | ZeroCap single-figure; min 20% below FibreCore; weekly review |
| `competitive_differentiation` | 2 | Anti-incumbent positioning; suburban/rural-first mission |
| `rollout_phasing` | 2 | 90-day sprint; permit registry monitoring for first-mover zones |
| `install_time_slas` | 2 | 5 business-day install guarantee; 45-day cluster build target |
| `churn_reduction` | 2 | <8% annual churn; 24-month price lock; loyalty credits at month 12 |
| `rural_coverage` | 2 | 100Mbps minimum; 1Gbps by end 2026; 90-day performance guarantee |
| `customer_acquisition` | 2 | AcquisitionBlitz geo-fenced digital + door-to-door; ambassador programme |
| `target_demographics` | 1 | Value-conscious suburban families; home workers; SMBs |

SwiftFibre total: 15 chunks

---

## EMBEDDING MODEL NOTES

**Provider-agnostic:** The column is `vector` (no dimension suffix), so any model output dimension is accepted.

Guidance by provider:
- **OpenAI `text-embedding-ada-002`:** 1536 dims, dense, cosine-friendly. Max input: 8191 tokens.
- **OpenAI `text-embedding-3-large`:** 3072 dims (or reduced). Better accuracy than ada-002.
- **Ollama `nomic-embed-text`:** 768 dims. Fast, local, no API cost.
- **Ollama `mxbai-embed-large`:** 1024 dims. Strong multilingual support.

**Important:** All chunks for a tenant must be embedded with the **same model and dimension**. Changing the provider config invalidates existing vectors — you must clear artifact chunks and re-ingest after changing providers.

Query design guidance:
- Natural-language phrases work better than keywords
- Queries and chunks use the same model (symmetric encoding)
- Queries about absent topics return the closest available chunks — callers must evaluate relevance
- No `category` filter via API; for category-filtered vector search, use direct SQL:
  ```sql
  WHERE "TenantId" = @id AND "Category" = @cat ORDER BY "Embedding" <-> @vec LIMIT @k
  ```

---

## ARTIFACT MANAGEMENT OPERATIONS

### List artifacts with chunk counts
```
GET /api/artifacts
X-Tenant-Id: <uuid>
```

### List chunks in an artifact
```
GET /api/artifacts/{artifactId}/contexts
```

### Clear chunks for re-ingestion
```
DELETE /api/artifacts/{artifactId}/contexts
→ 204 No Content
```
Artifact structure (name, description, department assignment) is preserved. Only `BusinessContext` rows are deleted.

### Run department discovery
```
POST /api/tenants/{tenantId}/discover-departments
→ { wasAiAssisted: bool, departments: [...], artifacts: [...] }
```
Idempotent — safe to run multiple times. Skips departments/artifacts whose names already exist.

---

## BULK INGEST — SEED ENDPOINT

```
POST /api/dev/seed
```

No body, no headers required. Returns:
```json
{
  "tenantsCreated":        0–2,
  "projectsCreated":       0–6,
  "requirementsCreated":   0–28,
  "contextChunksIngested": 0–30
}
```

Availability: Development environment only  
Idempotent: already-seeded data is skipped  
Provider requirement: embedding provider must be configured before seeding
