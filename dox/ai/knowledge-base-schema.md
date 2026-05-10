# knowledge-base-schema.md
# AI Agent Reference — Knowledge Base (BusinessContext) Schema & Operations

---

## BusinessContext — Full Schema

Table: `BusinessContexts`

| Field     | C# Type       | DB Type       | Nullable | Notes                              |
|-----------|---------------|---------------|----------|------------------------------------|
| Id        | Guid          | uuid          | NO       | PK; assigned by service (NewGuid) |
| TenantId  | Guid          | uuid          | NO       | FK → Tenants.Id; indexed           |
| Text      | string        | text          | NO       | The raw content chunk              |
| Embedding | Vector        | vector(1536)  | NO       | pgvector; never returned via HTTP  |
| Source    | string?       | text          | YES      | Free-form provenance string        |
| Category  | string?       | text          | YES      | Free-form classification label     |
| CreatedAt | DateTime      | timestamptz   | NO       | UTC; set by service at ingest time |

**Embedding model:** OpenAI `text-embedding-ada-002`
**Embedding dimensions:** 1536
**Distance metric used at query time:** L2 (Euclidean) — pgvector `<->` operator
**No vector index configured** — full sequential scan on `Embedding` column; `TenantId` B-tree index applied first as relational pre-filter

---

## HOW TO INGEST

**Endpoint:** `POST /api/business-context`
**Content-Type:** `application/json`

Required header:
```
X-Tenant-Id: <uuid>
```

Request body (typed):
```json
{
  "text":     "<string, required>",
  "source":   "<string | null>",
  "category": "<string | null>"
}
```

Constraints:
- `text` must be non-null (model binding enforces; null → 400)
- `source` and `category` are optional; omit or pass `null`
- `tenantId` must be a valid GUID; invalid format → 400 (model binding)
- Tenant existence is NOT validated on ingest — unknown tenant GUIDs are accepted and stored

Successful response `200 OK`:
```json
{
  "id":        "<uuid>",
  "tenantId":  "<uuid>",
  "createdAt": "<ISO 8601 UTC>"
}
```

Side effects per call:
1. One OpenAI API call to `text-embedding-ada-002` (latency ~200–600ms, billed per token)
2. One INSERT into `BusinessContexts`

---

## HOW TO SEARCH

**Endpoint:** `GET /api/business-context/search`

Required header:
```
X-Tenant-Id: <uuid>
```

Query parameters:
| Parameter | Type | Required | Default | Constraint |
|-----------|------|----------|---------|------------|
| query     | string | YES | — | URL-encoded; passed to embedding model |
| topK      | int  | NO  | 5 | No server-side maximum enforced |

Example:
```
GET /api/business-context/search?query=pricing+strategy&topK=3
X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1
```

Response `200 OK` — ordered array (closest first by L2 distance):
```json
[
  {
    "id":        "<uuid>",
    "text":      "<original chunk text>",
    "source":    "<string | null>",
    "category":  "<string | null>",
    "createdAt": "<ISO 8601 UTC>"
  }
]
```

Result ordering: ascending L2 distance from query embedding (most semantically similar first)
Result count: exactly `min(topK, matching_rows_for_tenant)`
Empty result: `[]` (not an error)

What the response means:
- Results are the `topK` closest stored chunks to the query within the tenant's data
- No score/distance value is returned — only ranked text
- All results belong to the requested tenant (enforced by `WHERE TenantId = @tenantId`)
- `Embedding` field is not included in response

Side effects per call:
1. One OpenAI API call to `text-embedding-ada-002` to embed the query
2. One read-only DB query

---

## TENANT SCOPING RULES

**Header:** `X-Tenant-Id` (type: Guid, format: standard UUID string)

Behavior table:

| Scenario | Ingest result | Search result |
|---|---|---|
| Valid GUID, tenant exists in DB | Data stored under that tenant | Returns only that tenant's chunks |
| Valid GUID, tenant does NOT exist in DB | Data stored (no FK check fails at insert) | Returns empty `[]` |
| Missing header | 400 Bad Request (model binding fails on required Guid) | 400 Bad Request |
| Malformed GUID (not parseable) | 400 Bad Request | 400 Bad Request |
| Valid GUID belonging to a different tenant's data | Full access — no ownership enforcement | Full access — returns their data |

**Critical:** There is no authentication. Any caller that knows (or guesses) a tenant GUID can read or write that tenant's data. The isolation is purely logical (SQL WHERE clause), not cryptographic or session-bound.

---

## CATEGORY TAXONOMY — SEED DATA

These are the exact `category` string values present after seeding. No enum constraint exists — any string is valid at ingest time.

### FibreCore Networks (TenantId: `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`)

| Category value | Chunk count | Topics covered |
|---|---|---|
| `pricing_strategy` | 2 | Residential premium positioning; enterprise rate card and discount policy |
| `target_demographics` | 2 | Residential (dual-income metro families); enterprise (50+ employee orgs, FS/legal/health/gov) |
| `rollout_phasing` | 3 | 4-phase FTTP plan (Q4 2026 metro → Q2 2028 secondary → suburban → rural); sub-zone scoring; disclosure policy |
| `sla_commitments` | 2 | Residential 99.9% availability, 4h/24h restoration; Enterprise Platinum 99.95%, 2h restoration, credit schedule |
| `competitive_response` | 2 | No reactive price matching; 5pp share-loss threshold triggers Executive Committee review |
| `partnership_approach` | 1 | Selective retailer/SI/civil contractor panel; annual accreditation |
| `regulatory_compliance` | 3 | Open-access obligations; Critical Infrastructure Protection; Data Protection Act (24-month retention) |

**FibreCore total chunks:** 15

---

### SwiftFibre (TenantId: `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`)

| Category value | Chunk count | Topics covered |
|---|---|---|
| `pricing_strategy` | 2 | ZeroCap single-figure pricing; min 20% below FibreCore; weekly price review |
| `competitive_differentiation` | 2 | Anti-incumbent positioning; suburban/rural-first mission |
| `rollout_phasing` | 2 | 90-day sprint model; full-zone commitment; permit registry monitoring |
| `install_time_slas` | 2 | 5 business-day install guarantee with 1-month credit on miss; 45-day street-cluster target |
| `churn_reduction` | 2 | <8% annual churn target; proactive monitoring; 24-month price lock; loyalty at month 12 |
| `rural_coverage` | 2 | 100Mbps minimum at launch; 1Gbps by end 2026; 90-day performance guarantee |
| `customer_acquisition` | 2 | AcquisitionBlitz geo-fenced digital + door-to-door; community ambassador programme |
| `target_demographics` | 1 | Value-conscious suburban families (FibreCore switchers); home workers; SMBs |

**SwiftFibre total chunks:** 15

---

## CROSS-TENANT CATEGORY OVERLAP

Categories present in BOTH tenants:
- `pricing_strategy`
- `target_demographics`
- `rollout_phasing`

Categories unique to FibreCore:
- `sla_commitments`
- `competitive_response`
- `partnership_approach`
- `regulatory_compliance`

Categories unique to SwiftFibre:
- `competitive_differentiation`
- `install_time_slas`
- `churn_reduction`
- `rural_coverage`
- `customer_acquisition`

---

## EMBEDDING MODEL — QUERY DESIGN IMPLICATIONS

**Model:** `text-embedding-ada-002` (OpenAI)
**Dimensions:** 1536
**Type:** Dense vector, cosine-similarity-friendly (L2 used here — equivalent ranking when vectors normalized)
**Max input tokens:** 8191 tokens (OpenAI limit for ada-002)
**Language:** English only (seed data is English)

Query design guidance:
- Queries should be natural-language phrases describing the information needed, not keywords
- Short queries (3–15 words) and full sentences both work; ada-002 handles both well
- Category filtering is NOT available via the search endpoint — no `?category=` filter exists; filtering requires direct DB access
- To search by category, use direct SQL: `WHERE TenantId = @id AND Category = @cat ORDER BY Embedding <-> @vec LIMIT @k`
- The same embedding is used for both stored chunks and queries — no asymmetric encoding
- Queries about topics not in the knowledge base return the closest available chunks, not an empty result; callers must evaluate semantic relevance of returned text

---

## BULK INGEST — SEED ENDPOINT

To populate both tenants with all 30 context chunks, projects, and requirements in one call:

```
POST /api/dev/seed
```

No body, no headers required. Returns:
```json
{
  "tenantsCreated": 0–2,
  "projectsCreated": 0–6,
  "requirementsCreated": 0–28,
  "contextChunksIngested": 0–30
}
```

Availability: Development environment only (controlled by `ASPNETCORE_ENVIRONMENT=Development`)
Idempotent: safe to call multiple times; already-seeded data is skipped
Cost: up to 30 OpenAI embedding API calls on first run; 0 if already seeded
