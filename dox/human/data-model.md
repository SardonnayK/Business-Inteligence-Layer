# Data Model

## Entity relationships

The schema has four tables. The relationships form two parallel trees that both root at `Tenant`.

```
Tenant
├── Project (one tenant → many projects)
│   └── Requirement (one project → many requirements)
└── BusinessContext (one tenant → many context chunks)
```

`Project` and `BusinessContext` are siblings: they both belong to a `Tenant` directly but they serve different purposes. Projects and Requirements are the structured, relational side — what the tenant is building and what rules govern it. BusinessContext is the unstructured, vector side — the searchable knowledge base.

---

## Entity: Tenant

A Tenant is a single organisation using the platform. Everything is scoped to a tenant. No data crosses tenant boundaries at query time.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | Assigned by the seeder for known tenants; generated with `Guid.NewGuid()` for tenants added via API |
| `Name` | `text` | The organisation's display name |
| `IsActive` | `boolean` | Default `true`. The guardrail service rejects AI output for inactive tenants |
| `CreatedAt` | `timestamptz` | Set to `DateTime.UtcNow` on insertion |

The `IsActive` flag is the only tenant-level guardrail control today. There is no per-tenant policy configuration yet, though the guardrail service is explicitly designed as an extension point for adding that.

---

## Entity: Project

A Project is a workstream or initiative within a tenant. It exists primarily to give Requirements a meaningful grouping.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | |
| `Name` | `text` | Short project title |
| `Description` | `text` | A paragraph describing the project's scope and goals |
| `CreatedAt` | `timestamptz` | |

Projects do not participate in the vector search. They are structural containers for Requirements.

---

## Entity: Requirement

A Requirement is a concrete business or system rule attached to a Project. These represent things the software must enforce — process rules, approval thresholds, SLA commitments, legal constraints.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `ProjectId` | `uuid` (FK → Project) | |
| `Content` | `text` | The requirement statement in full |
| `Status` | `text` | One of `Draft`, `In Review`, `Approved` (by convention; not enforced by the database) |
| `CreatedAt` | `timestamptz` | |

Requirements are not embedded and are not searchable via the vector search endpoint. They are stored relationally and can be queried via the database directly or exposed through future API endpoints. There is no Requirements API endpoint today.

---

## Entity: BusinessContext

A BusinessContext record is a single chunk of text from a tenant's internal knowledge base, together with its vector embedding. This is the core of the RAG (Retrieval-Augmented Generation) capability.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | The isolation key — every query filters on this |
| `Text` | `text` | The original human-readable text |
| `Embedding` | `vector(1536)` | The OpenAI `text-embedding-ada-002` embedding. Stored as a pgvector column. |
| `Source` | `text` (nullable) | A label for where the text came from (document name, URL, etc.) |
| `Category` | `text` (nullable) | A topic label (e.g. `pricing_strategy`, `sla_commitments`) |
| `CreatedAt` | `timestamptz` | |

The `vector(1536)` column type is provided by the pgvector PostgreSQL extension, which is enabled on startup via the `CREATE EXTENSION vector` migration. The L2 (Euclidean) distance operator `<->` is used to rank results by semantic similarity.

### Why chunks rather than whole documents?

Large documents contain many different ideas. If you embed an entire pricing policy document as a single vector, the resulting embedding is an average of all the concepts in it, which makes it a poor match for any specific question. By splitting the document into paragraphs or sections — each covering a single coherent idea — each embedding is much more focused, and the similarity ranking returns genuinely relevant passages rather than entire documents that happen to touch the topic somewhere.

The seeded data uses this approach explicitly. A single source document like `"FibreCore Competitive Response Playbook v1.1"` is split into two chunks: one about the day-to-day policy (no reactive price matching, retention campaigns instead) and one about the escalation path (Executive Committee review when market share loss exceeds 5 percentage points over two quarters). A search for "how do we respond to market share loss" will surface the escalation chunk over the retention campaign chunk because they have different semantic content.

The `source` and `category` fields let you trace each chunk back to its origin document and group chunks by topic without re-reading the text.

---

## The two seeded companies

### FibreCore Networks

**Character:** Incumbent operator. 75 % market share. Methodical, premium-priced, urban-first. Deliberate competitive-response posture.

**Tenant ID:** `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`

**Projects:**

| Project | ID | Description |
|---|---|---|
| Urban FTTP Rollout | `a1a1a1a1-0000-0000-0000-000000000010` | Phased fibre rollout across the top 8 metro areas, prioritising zones by ARPU and density |
| Enterprise Fibre Sales Programme | `a1a1a1a1-0000-0000-0000-000000000020` | Go-to-market motion targeting large enterprise and government accounts |
| National Backbone Capacity Upgrade | `a1a1a1a1-0000-0000-0000-000000000030` | Upgrade of core nodes to 10G/25G with redundant ring topology |

**Requirements by project:** 5 for Urban Rollout, 4 for Enterprise Sales, 4 for Backbone Upgrade — 13 total. They cover topics such as zone-scoring formulas, CFO countersignature thresholds for large contracts, 30-day installation lead times, 99.95 % enterprise SLA guarantees, change-freeze calendars for maintenance, and vendor security review processes.

**Context chunks:** 15 chunks across 6 categories:

| Category | Number of chunks | What they cover |
|---|---|---|
| `pricing_strategy` | 2 | Premium residential positioning; Pricing Committee approval for enterprise discounts |
| `target_demographics` | 2 | Dual-income metro households; enterprise clients in finance, legal, healthcare, government |
| `rollout_phasing` | 3 | Four-phase rollout plan; sub-zone scoring model; policy against publishing uncommitted coverage areas |
| `sla_commitments` | 2 | 99.9 % residential SLA; 99.95 % Platinum enterprise SLA with credit schedule |
| `competitive_response` | 2 | No reactive price matching; Executive Committee escalation threshold |
| `partnership_approach` | 1 | Selective, accredited partner panel |
| `regulatory_compliance` | 3 | Open-access obligations; Critical Infrastructure Protection; Data Protection Act |

### SwiftFibre

**Character:** Challenger operator. 25 % market share and growing. Aggressive, value-priced, suburban-first. Explicitly positions against FibreCore.

**Tenant ID:** `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`

**Projects:**

| Project | ID | Description |
|---|---|---|
| SuburbanEdge Rollout Programme | `b2b2b2b2-0000-0000-0000-000000000010` | Aggressive FTTP expansion into suburban/peri-urban zones FibreCore has deprioritised |
| ZeroCap Disruptive Pricing Programme | `b2b2b2b2-0000-0000-0000-000000000020` | No-lock-in pricing at least 20 % below FibreCore's equivalent plans |
| AcquisitionBlitz Customer Growth Initiative | `b2b2b2b2-0000-0000-0000-000000000030` | Multi-channel acquisition campaign targeting FibreCore switchers |

**Requirements by project:** 5 for SuburbanEdge, 5 for ZeroCap Pricing, 5 for AcquisitionBlitz — 15 total. They cover topics such as first-mover advantage in uncovered postcodes, 45-day install targets with executive escalation at 60 days, 35 % take-up rate minimums, all-inclusive pricing with no add-on fees at checkout, competitive comparison tools, same-week installation guarantees, and geo-fenced ad spend.

**Context chunks:** 15 chunks across 6 categories:

| Category | Number of chunks | What they cover |
|---|---|---|
| `pricing_strategy` | 2 | ZeroCap all-inclusive philosophy; 20 % price floor below FibreCore with weekly review |
| `competitive_differentiation` | 2 | Brand positioning against FibreCore; mission to serve underserved communities |
| `rollout_phasing` | 2 | 90-day sprint structure; permit-registry monitoring for first-mover zones |
| `install_time_slas` | 2 | 5-business-day installation guarantee; 45-day cluster build target |
| `churn_reduction` | 2 | Sub-8 % annual churn target; 24-month price lock as primary retention tool |
| `rural_coverage` | 2 | 100 Mbps minimum at rural launch; 90-day performance guarantee |
| `customer_acquisition` | 2 | AcquisitionBlitz geo-fenced advertising; community ambassador programme |
| `target_demographics` | 1 | Value-conscious suburban families; home-based workers and small businesses |

---

## Tenant isolation in practice

The hybrid query in `HybridRagService.SearchAsync` is:

```sql
SELECT * FROM "BusinessContexts"
WHERE "TenantId" = @tenantId
ORDER BY "Embedding" <-> @queryVector
LIMIT @topK
```

The `WHERE` clause runs first. PostgreSQL never even computes distances for rows belonging to another tenant. This means isolation is enforced at the database level, not just in application logic. The integration tests explicitly verify this with a `Search_DoesNotLeakAcrossTenants` test that seeds data for tenant A and confirms tenant B gets an empty result set.

The `TenantId` passed to the query comes from the `X-Tenant-Id` HTTP header. The system trusts this header — there is no token validation or signature check in the current implementation.
