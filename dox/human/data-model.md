# Data Model

## Entity relationships

The schema has nine tables. The core of the system is a four-level hierarchy rooted at `Tenant`.

```
Tenant
├── EmbeddingProviderConfig (optional; one tenant-specific or one system-default)
├── DepartmentManifest (optional; one per tenant; free-text guidance blob for the AI supervisor)
├── Department (zero or many; discovered by AI or created manually)
│   └── ArtifactDepartment (join table — one Artifact may belong to many Departments)
│       └── Artifact
│           └── BusinessContext (chunks with vector embeddings)
├── Artifact [IsShared=true] (exactly one per tenant; not owned by any department)
│   └── BusinessContext
├── Project (workstream container)
│   └── Requirement (rules and constraints)
```

`Department` → `ArtifactDepartment` → `Artifact` → `BusinessContext` is the knowledge hierarchy. An artifact may be linked to more than one department via the `ArtifactDepartment` join table. `Project` and `Requirement` are the structured, relational side — they exist for reference and are not involved in the RAG search path.

---

## Entity: Tenant

An organisation using the platform. Everything is scoped to a tenant. No data crosses tenant boundaries at query time.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | Fixed GUIDs for seeded tenants; `Guid.NewGuid()` otherwise |
| `Name` | `text` | Display name |
| `IsActive` | `boolean` | Default `true`. Guardrail middleware returns 422 for inactive tenants on `/api/generate/*` |
| `CreatedAt` | `timestamptz` | Set to `DateTime.UtcNow` on insertion |

---

## Entity: EmbeddingProviderConfig

Per-tenant (or system-wide default) configuration for AI providers. Determines which embedding model generates vectors and which chat model powers the AI supervisor.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant, nullable) | `null` means this is the system-wide default config |
| `ProviderType` | `enum` | `None`, `OpenAI`, `AzureOpenAI`, `Ollama` |
| `ModelId` | `text` | Embedding model name (e.g. `text-embedding-ada-002`, `nomic-embed-text`) |
| `ApiKey` | `text` (nullable) | Required for OpenAI and Azure OpenAI; not used for Ollama |
| `Endpoint` | `text` (nullable) | Required for Azure OpenAI; optional override for OpenAI; Ollama base URL |
| `ChatModelId` | `text` (nullable) | The generation model for the AI supervisor (e.g. `gpt-4o`, `llama3`). When null, supervisor falls back to the shared artifact without LLM routing |
| `UpdatedAt` | `timestamptz` | Updated on every PUT |

**Resolution order at request time:**
1. Tenant-specific config where `TenantId = requestTenantId`
2. System default where `TenantId IS NULL`
3. If neither exists → 500: `"No embedding provider configured"`

The API key is never returned by the API — the `GET` endpoints return a `hasApiKey: bool` flag instead.

---

## Entity: DepartmentManifest

A free-text guidance blob stored per tenant. The AI supervisor reads this manifest when making routing and discovery decisions, so it provides a stable description of what departments exist and what they should contain. Markdown is recommended for readability.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant, unique) | One manifest per tenant |
| `Content` | `text` | Free-text (Markdown recommended). Describes the tenant's department structure and routing guidance for the supervisor |
| `UpdatedAt` | `timestamptz` | Updated on every PUT |

The manifest is optional. When absent, the supervisor relies solely on the existing artifact catalog. When present, it overrides or supplements the catalog descriptions to steer routing.

**Example manifest content:**

```markdown
## Departments

- **Engineering** — technical standards, architecture decisions, and development processes
- **HR** — policies, recruitment, and employee lifecycle
- **Sales** — playbooks, pricing guidance, and competitive positioning
- **Legal** — contracts, compliance obligations, and regulatory requirements

## Routing notes

Content mentioning SLAs should go to Engineering unless it is a commercial commitment, in which case route to Sales.
Cross-cutting strategy content belongs in the shared artifact.
```

---

## Entity: Department

A logical division within a tenant. Departments are discovered automatically by the AI supervisor analyzing existing knowledge, or created manually. Artifacts are linked to departments via the `ArtifactDepartment` join table, so a single artifact may appear in multiple departments.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | |
| `Name` | `text` | Short department name (e.g. `Engineering`, `HR`, `Sales`) |
| `Description` | `text` | What knowledge this department holds |
| `EstimatedSize` | `text` | `"small"`, `"medium"`, or `"large"` — set by the AI when the department is discovered |
| `CreatedAt` | `timestamptz` | |

The `EstimatedSize` field is a hint to the supervisor, but artifact counts are no longer hardcoded to size. The tenant's `DepartmentManifest` takes precedence: when a manifest is present its guidance determines how many and what kind of artifacts belong in a department. In the absence of a manifest the supervisor uses its own judgement based on content volume and semantic coherence.

---

## Entity: ArtifactDepartment

Join table that links artifacts to departments. Replaces the former `DepartmentId` foreign key on `Artifact`, allowing an artifact to belong to more than one department.

| Column | Type | Notes |
|---|---|---|
| `ArtifactId` | `uuid` (FK → Artifact) | Composite PK |
| `DepartmentId` | `uuid` (FK → Department) | Composite PK |

The composite primary key `(ArtifactId, DepartmentId)` prevents duplicate links. Deleting a `Department` or `Artifact` cascades the corresponding join rows.

---

## Entity: Artifact

A scoped knowledge document that may belong to one or more departments (via `ArtifactDepartment`), or be the shared cross-cutting artifact. Each artifact is the container for a set of related `BusinessContext` chunks.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | |
| `Name` | `text` | Document title (e.g. `"Technical Standards"`, `"Company Knowledge"`) |
| `Description` | `text` | What this artifact holds |
| `IsShared` | `boolean` | `true` only for the single shared/cross-cutting artifact per tenant |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

Department membership is recorded in the `ArtifactDepartment` join table, not as a column on this entity. API responses expose department membership as `departments: [{id, name}]` — an array, because an artifact may belong to more than one department.

**Shared artifact:** Every tenant has exactly one artifact where `IsShared = true`. This artifact stores company-wide goals, mission statements, cross-cutting policies, and anything all departments must work toward together. It is created automatically on first ingest if no artifacts exist yet. The shared artifact has no entries in `ArtifactDepartment`.

---

## Entity: BusinessContext

A single chunk of text from a tenant's knowledge base, together with its vector embedding. This is the leaf node of the knowledge hierarchy and the core of the RAG capability.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | The primary isolation key |
| `ArtifactId` | `uuid` (FK → Artifact, nullable) | Set by the AI supervisor at ingest time; null for legacy data before the artifact system was introduced |
| `Text` | `text` | The original human-readable text |
| `Embedding` | `vector` | Dimensionless pgvector column — accepts any provider's output dimension |
| `Source` | `text` (nullable) | Where the text came from (document name, URL, etc.) |
| `Category` | `text` (nullable) | A topic label (e.g. `pricing_strategy`, `sla_commitments`) |
| `CreatedAt` | `timestamptz` | |

The `Embedding` column uses a dimensionless `vector` type (not `vector(1536)`) so the system can accommodate any provider — OpenAI ada-002 outputs 1536 dimensions, but Ollama `nomic-embed-text` outputs 768, and future models may differ.

### Why chunks rather than whole documents?

Large documents contain many different ideas. Embedding an entire policy document produces a vector that is an average of all concepts in it, making it a poor match for any specific question. Splitting the document into focused paragraphs — each covering a single coherent idea — gives each embedding a sharp semantic focus. The supervisor routes each chunk to the right artifact so similar ideas stay together, further improving retrieval precision.

---

## Entity: Project

A workstream or initiative within a tenant. Structural container for Requirements.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `TenantId` | `uuid` (FK → Tenant) | |
| `Name` | `text` | Short project title |
| `Description` | `text` | Scope and goals |
| `CreatedAt` | `timestamptz` | |

Projects do not participate in vector search.

---

## Entity: Requirement

A concrete business or system rule attached to a Project.

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` (PK) | |
| `ProjectId` | `uuid` (FK → Project) | |
| `Content` | `text` | The requirement in full |
| `Status` | `text` | `"Draft"`, `"In Review"`, or `"Approved"` (convention only; not enforced by DB) |
| `CreatedAt` | `timestamptz` | |

Requirements are not embedded and not searchable via the vector search endpoint.

---

## The two seeded companies

### FibreCore Networks

**Character:** Incumbent operator. 75% market share. Methodical, premium-priced, urban-first. Deliberate competitive-response posture.

**Tenant ID:** `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`

Projects: Urban FTTP Rollout, Enterprise Fibre Sales Programme, National Backbone Capacity Upgrade — 13 requirements total.

**Context chunks:** 15 chunks across 7 categories: `pricing_strategy`, `target_demographics`, `rollout_phasing`, `sla_commitments`, `competitive_response`, `partnership_approach`, `regulatory_compliance`.

### SwiftFibre

**Character:** Challenger operator. 25% market share and growing. Aggressive, value-priced, suburban-first. Explicitly positions against FibreCore.

**Tenant ID:** `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`

Projects: SuburbanEdge Rollout Programme, ZeroCap Disruptive Pricing Programme, AcquisitionBlitz Customer Growth Initiative — 15 requirements total.

**Context chunks:** 15 chunks across 8 categories: `pricing_strategy`, `competitive_differentiation`, `rollout_phasing`, `install_time_slas`, `churn_reduction`, `rural_coverage`, `customer_acquisition`, `target_demographics`.

---

## Tenant isolation in practice

Every query through `HybridRagService` filters by `TenantId` before computing vector distances:

```sql
SELECT bc.* FROM "BusinessContexts" bc
WHERE bc."TenantId" = @tenantId                   -- relational pre-filter
  [AND bc."ArtifactId" = @artifactId]             -- optional artifact scope
  [AND EXISTS (                                   -- optional department scope
      SELECT 1 FROM "ArtifactDepartments" ad
      WHERE ad."ArtifactId" = bc."ArtifactId"
        AND ad."DepartmentId" = @deptId
  )]
ORDER BY bc."Embedding" <-> @queryVector          -- vector distance
LIMIT @topK
```

The `WHERE` clause runs first. PostgreSQL never computes distances for rows belonging to another tenant. Tenant isolation is enforced at the database level, not just in application logic. The integration tests explicitly verify this with a `Search_DoesNotLeakAcrossTenants` test.

The `TenantId` comes from the `X-Tenant-Id` HTTP header. The system currently trusts this header — there is no token validation or signature check.
