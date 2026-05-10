# Overview

## What this system is

The Business Intelligence Layer is a multi-tenant RAG (Retrieval-Augmented Generation) platform. Each organisation ("tenant") maintains its own private knowledge base — internal policies, strategy documents, operational rules, regulatory constraints. The platform converts that knowledge into vector embeddings, organises it into a structured hierarchy of departments and artifacts, and retrieves the most relevant context on demand using semantic search.

The system is **embedding-provider agnostic**: it works with OpenAI, Azure OpenAI, local Ollama models, or any compatible API. No specific vendor is required at startup — providers are configured through the API and stored per tenant.

An **AI supervisor** runs on every ingestion. Rather than dumping all knowledge into a single flat pool, the supervisor routes each piece of text to the correct *artifact* (a scoped knowledge document) within the right department, keeping the knowledge base organised automatically as it grows.

---

## Key concepts

| Concept | Description |
|---|---|
| **Tenant** | An organisation using the platform. All data is isolated by tenant ID. |
| **Department** | A division within a tenant (e.g. Engineering, HR, Sales). Discovered automatically by the AI or created manually. |
| **Artifact** | A scoped knowledge document within a department. A large department may have several artifacts; a small one has one. |
| **Shared Artifact** | One special artifact per tenant, not owned by any department — holds company-wide goals, mission, and cross-cutting policies. |
| **BusinessContext** | A single text chunk with its vector embedding. Always stored inside an artifact. |
| **Supervisor** | An LLM-powered routing agent. On each ingest it reads the text, looks at the tenant's artifact catalog, and decides where the new content belongs. |
| **EmbeddingProviderConfig** | Per-tenant (or system-wide default) configuration selecting which AI provider and model to use for both embeddings and the supervisor LLM. |

---

## System architecture

```mermaid
graph TD
    subgraph Aspire["Aspire Orchestration"]
        AppHost["Orchestrator.AppHost"]
    end

    subgraph App["Application (Clean Architecture)"]
        Api["Orchestrator.Api\nASP.NET Core 10"]
        Engine["Orchestrator.Engine\nSemantic Kernel Plugin"]
        Infra["Orchestrator.Infrastructure\nEF Core · Services · Migrations"]
        Core["Orchestrator.Core\nEntities · Interfaces"]
    end

    subgraph Frontend["Dashboard"]
        UI["React + Vite + TypeScript\nsrc/dashboard"]
    end

    subgraph Shared["Shared"]
        Defaults["Orchestrator.ServiceDefaults\nOTel · Health · Resilience"]
    end

    subgraph Data["Data"]
        PG[("PostgreSQL 16\n+ pgvector")]
    end

    subgraph Providers["AI Providers (pluggable)"]
        OAI["OpenAI / Azure OpenAI"]
        Ollama["Ollama (local)"]
    end

    AppHost -->|starts + wires| Api
    AppHost -->|starts| UI
    Api --> Engine
    Api --> Infra
    Api --> Defaults
    Engine --> Core
    Infra --> Core
    Infra --> PG
    Infra -->|at runtime| OAI
    Infra -->|at runtime| Ollama
    UI -->|HTTP| Api
```

**Dependency direction:** `AppHost → Api → Engine → Core ← Infrastructure ← Api`

The `Core` project has zero external dependencies — no database drivers, no HTTP clients, no AI SDKs. Everything external lives in `Infrastructure`.

---

## Knowledge organisation

Knowledge within a tenant is structured as a three-level hierarchy:

```mermaid
graph TD
    T["🏢 Tenant"]

    T --> SA["📌 Shared Artifact\nCompany Knowledge\nIsShared = true"]
    T --> D1["📂 Engineering\nsize: large"]
    T --> D2["📂 HR\nsize: medium"]
    T --> D3["📂 Sales\nsize: small"]

    D1 --> A1["📄 Technical Standards\n12 chunks"]
    D1 --> A2["📄 Engineering Processes\n8 chunks"]
    D2 --> A3["📄 HR Policies\n6 chunks"]
    D2 --> A4["📄 Recruitment\n4 chunks"]
    D3 --> A5["📄 Sales Playbook\n9 chunks"]

    SA --> C0["BusinessContext chunks..."]
    A1 --> C1["BusinessContext chunks..."]
    A2 --> C2["BusinessContext chunks..."]
```

- The **shared artifact** is always present and spans all departments. It captures mission, values, strategic goals, and anything that every department must work toward together.
- Departments are sized by the AI as **small** (1 artifact), **medium** (2 artifacts), or **large** (3 artifacts). A large Engineering department splits its knowledge into focused artifacts (standards vs. processes) so each artifact stays semantically coherent.
- Every `BusinessContext` chunk belongs to exactly one artifact, inheriting its department scope.

---

## Ingest flow

When text is submitted to `POST /api/business-context`, the supervisor runs first:

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant Supervisor
    participant LLM
    participant DB
    participant Embedder

    Client->>API: POST /api/business-context\n{text, source, category}

    API->>Supervisor: RouteToArtifactAsync(text, tenantId)
    Supervisor->>DB: Load EmbeddingProviderConfig
    Supervisor->>DB: Load all Artifacts for tenant

    alt No artifacts exist yet
        Supervisor->>DB: Create shared "Company Knowledge" artifact
        Note over Supervisor: Bootstrap — first ingest
    else ChatModelId is configured
        Supervisor->>LLM: Route text to artifact\n(structured JSON prompt)
        LLM-->>Supervisor: {action: "route", artifactId: "..."}
        alt action = "create"
            Supervisor->>DB: Create Department + Artifact
        end
    else No ChatModelId
        Supervisor->>Supervisor: Fall back to shared artifact
    end

    Supervisor-->>API: Chosen Artifact

    API->>Embedder: GenerateAsync(text)
    Embedder-->>API: float[] vector

    API->>DB: INSERT BusinessContext\n(text, vector, artifactId, tenantId)
    API-->>Client: {id, artifactId, artifactName,\ndepartmentId, departmentName, isShared}
```

The response tells the caller exactly where the text was placed — which artifact and which department.

---

## Search flow

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant Embedder
    participant DB

    Client->>API: GET /api/business-context/search\n?query=...&topK=5\n[&artifactId=...] [&departmentId=...]

    API->>Embedder: GenerateAsync(query)
    Embedder-->>API: float[] queryVector

    alt artifactId supplied
        API->>DB: WHERE ArtifactId = @id\nORDER BY Embedding <-> @vec\nLIMIT topK
    else departmentId supplied
        API->>DB: WHERE Artifact.DepartmentId = @id\nORDER BY Embedding <-> @vec\nLIMIT topK
    else tenant-wide
        API->>DB: WHERE TenantId = @id\nORDER BY Embedding <-> @vec\nLIMIT topK
    end

    DB-->>API: Ranked BusinessContext rows
    API-->>Client: [{id, text, source, category,\nartifactId, artifactName, createdAt}]
```

Search is always tenant-scoped. The optional `artifactId` or `departmentId` parameters narrow the scope further, which is useful for department-specific retrieval.

---

## Department discovery flow

```mermaid
sequenceDiagram
    participant User
    participant Dashboard
    participant API
    participant Supervisor
    participant LLM
    participant DB

    User->>Dashboard: Click "Discover Departments"
    Dashboard->>API: POST /api/tenants/{id}/discover-departments

    API->>Supervisor: DiscoverDepartmentsAsync(tenantId)
    Supervisor->>DB: Sample up to 30 existing chunks

    alt ChatModelId configured
        Supervisor->>LLM: Identify departments + artifact count\n(structured JSON prompt)
        LLM-->>Supervisor: {departments: [...], sharedArtifact: {...}}
        Supervisor->>DB: Upsert Departments + Artifacts
        Supervisor-->>API: DiscoveryResult {wasAiAssisted: true}
    else No ChatModelId
        Supervisor->>DB: Create "General" dept + artifact
        Supervisor-->>API: DiscoveryResult {wasAiAssisted: false}
    end

    API-->>Dashboard: {wasAiAssisted, departments, artifacts}
    Dashboard->>User: Shows tree + "AI-assisted: yes/no" banner
```

Discovery is idempotent — running it again skips departments and artifacts whose names already exist.

---

## Provider resolution

```mermaid
flowchart TD
    A["Ingest or Search request\nfor tenantId"] --> B{"Tenant-specific\nEmbeddingProviderConfig?"}
    B -- Yes --> D["Use tenant config"]
    B -- No --> C{"System default\nconfig (TenantId = NULL)?"}
    C -- Yes --> D
    C -- No --> E["Throw: 'No embedding\nprovider configured'"]
    D --> F{"ProviderType?"}
    F -- OpenAI --> G["OpenAIClient\n+ EmbeddingClient\n.AsIEmbeddingGenerator()"]
    F -- AzureOpenAI --> H["OpenAIClient (Azure endpoint)\n+ EmbeddingClient\n.AsIEmbeddingGenerator()"]
    F -- Ollama --> I["OllamaApiClient\nimplements IEmbeddingGenerator\ndirectly"]
    F -- None --> J["Throw: 'Configure provider\nat /api/embedding-config'"]
    G & H & I --> K["Cache generator\nin ConcurrentDictionary"]
    K --> L["Generate embeddings"]
```

The same provider config is used for both the **embedding model** (`ModelId`) and the **supervisor LLM** (`ChatModelId`). They share the same provider type, API key, and endpoint — only the model name differs.

---

## The two seeded companies

The demonstration data uses two competing broadband companies to show multi-tenant isolation clearly.

**FibreCore Networks** — the incumbent. 75 % market share, urban-first, premium-priced. Deliberate, committee-driven competitive response. Tenant ID: `a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1`

**SwiftFibre** — the challenger. 25 % share and growing fast. At least 20 % below FibreCore on every plan, suburban and rural focus, 45-day build targets. Tenant ID: `b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2`

Both tenants share the same database tables but are completely isolated: every query is filtered by `TenantId` before vector distance is computed. The integration tests explicitly verify that a search for tenant A returns nothing for tenant B.

---

## What works today

| Capability | Status |
|---|---|
| Multi-tenant knowledge ingestion with supervisor routing | ✅ Working |
| Vendor-agnostic embedding (OpenAI, Azure OpenAI, Ollama) | ✅ Working |
| Per-tenant and system-default provider config | ✅ Working |
| Department discovery (AI-assisted or fallback) | ✅ Working |
| Artifact management and chunk clearing | ✅ Working |
| Hybrid RAG search (tenant / artifact / department scoped) | ✅ Working |
| React dashboard (all pages, settings, knowledge tree) | ✅ Working |
| Guardrail middleware for `/api/generate/*` routes | ✅ Wired — no `/api/generate` controllers yet |
| Semantic Kernel plugin (`BusinessContextPlugin`) | ✅ Defined — not registered in DI yet |
| Authentication / tenant ownership verification | ❌ Not implemented — header is trusted as-is |
| Vector index (IVFFlat / HNSW) for scale | ❌ Not configured — sequential scans at large scale |
