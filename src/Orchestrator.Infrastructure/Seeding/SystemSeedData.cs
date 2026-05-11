namespace Orchestrator.Infrastructure.Seeding;

internal static class SystemSeedData
{
    // ─── Orchestrator Platform (System Tenant) ────────────────────────────────
    // Self-documentation · enables the AI to answer questions about the platform
    // using its own RAG pipeline rather than relying on static prompts.

    internal static readonly Guid TenantId = new("c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3");
    internal const string TenantName = "Orchestrator Platform";

    internal static class Artifacts
    {
        internal static readonly Guid PlatformKnowledge = new("c3c3c3c3-0000-0000-0002-000000000000");
    }

    internal static class Projects
    {
        internal static readonly Guid PlatformCapabilities = new("c3c3c3c3-0000-0000-0000-000000000010");
        internal static readonly Guid IntegrationGuide     = new("c3c3c3c3-0000-0000-0000-000000000020");
    }

    internal static readonly SeedData.ArtifactSeed[] ArtifactSeeds =
    [
        new(Artifacts.PlatformKnowledge, TenantId, null,
            "Platform Knowledge",
            "All platform documentation: API surface, architecture, tenant model, authentication, knowledge management, and operational guidance.",
            IsShared: true),
    ];

    internal static readonly SeedData.ProjectSeed[] ProjectSeeds =
    [
        new(Projects.PlatformCapabilities, TenantId,
            "Platform Capabilities",
            "Documents what the Orchestrator platform can do: its API surface, RAG pipeline, vendor-agnostic embedding model, guardrail layer, AI supervisor, Semantic Kernel plugin, and Aspire hosting setup."),
        new(Projects.IntegrationGuide, TenantId,
            "Integration Guide",
            "Documents how to connect to, run, and extend the Orchestrator platform: tenant isolation model, authentication, available endpoints, local development setup, and the path forward for production readiness."),
    ];

    internal static readonly SeedData.RequirementSeed[] RequirementSeeds =
    [
        // Platform Capabilities
        new(new("c1000001-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The platform must expose a POST /api/business-context endpoint that accepts a text payload and JWT auth token. It must route the text via the AI supervisor to the correct artifact, generate an embedding via the configured embedding provider, and persist both the raw text and the vector to the BusinessContexts table.", "Approved"),
        new(new("c1000002-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The platform must expose a GET /api/business-context/search endpoint that accepts a natural-language query. It must embed the query using the tenant's configured provider, perform an L2 nearest-neighbour search against pgvector filtered to the authenticated tenant, and return the top-K matching chunks.", "Approved"),
        new(new("c1000003-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The POST /api/dev/seed endpoint (development only) must trigger the DataSeeder, which creates tenants, departments, artifacts, projects, requirements, and business-context embeddings for all registered seed datasets. The seeder must be idempotent: re-running must not create duplicates.", "Approved"),
        new(new("c1000004-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The guardrail pipeline must intercept every inbound request before it reaches the RAG search path. In the current stub implementation it passes all requests through unconditionally. The interface is defined so that a real policy engine can be wired in without changing downstream code.", "In Review"),
        new(new("c1000005-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The BusinessContextPlugin Semantic Kernel plugin must expose a search_business_context KernelFunction that accepts a query string, calls the hybrid RAG service, and returns a formatted string of matching context chunks.", "Draft"),
        new(new("c1000006-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "All services must expose /health/live and /health/ready endpoints following the Aspire ServiceDefaults pattern. The Aspire dashboard must aggregate health status across the API, AppHost, and dependent services.", "Approved"),

        // Integration Guide
        new(new("c2000001-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "Every API call that reads or writes tenant data must include a valid JWT Bearer token issued by POST /api/auth/login. The token carries a tenant_id claim that binds the caller to exactly one tenant. The platform enforces tenant isolation at both the JWT and database query level.", "Approved"),
        new(new("c2000002-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The local development environment can be started in three ways: (1) via the Aspire AppHost project which orchestrates all dependencies, (2) via docker-compose which starts Postgres with pgvector and the API, or (3) via the test.ps1 PowerShell script which runs integration tests against a live stack.", "Approved"),
        new(new("c2000003-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "New tenants must be registered before any ingest or search calls are made. The POST /api/dev/seed endpoint seeds the built-in demo tenants and creates their admin users automatically. Each seeded tenant's admin credentials are username: admin, password: Admin123!.", "In Review"),
        new(new("c2000004-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The platform does not currently expose a POST /api/generate endpoint. Text generation must be invoked by the caller using the Semantic Kernel BusinessContextPlugin together with a separately provisioned LLM. This separation keeps the BI layer stateless with respect to LLM orchestration.", "Draft"),
        new(new("c2000005-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The embedding provider is configured per-tenant via PUT /api/embedding-config/tenant/{id} or globally via PUT /api/embedding-config/system. Supported providers are OpenAI, Azure OpenAI, and Ollama (local). Tenants without a specific config inherit the system default.", "Approved"),
        new(new("c2000006-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "Department structure is defined per-tenant via a free-text manifest (PUT /api/tenants/{id}/department-manifest). The AI supervisor reads this manifest when routing ingested text and when running department discovery. Markdown headings in the manifest are auto-synced to Department rows.", "Approved"),
    ];

    internal static readonly SeedData.ContextChunkSeed[] ContextChunks =
    [
        // API surface
        new("The Orchestrator platform exposes the following primary endpoints. POST /api/auth/login issues a JWT token for a given username, password, and tenantId. POST /api/business-context ingests a text chunk: the AI supervisor routes it to the correct artifact, generates a vector embedding, and persists both to the database. GET /api/business-context/search performs hybrid vector + relational search returning the top-K semantically similar chunks. POST /api/dev/seed (dev mode only) seeds all demo tenants idempotently.",
            "Orchestrator Platform — API Reference v2.0", "api_endpoints",
            Artifacts.PlatformKnowledge),

        new("Artifact and department management endpoints: GET /api/artifacts lists all artifacts the caller can access. GET /api/departments lists all departments with artifact counts. POST /api/tenants/{id}/discover-departments runs AI-assisted department and artifact discovery against existing knowledge. POST /api/artifacts/{id}/departments/{deptId} and DELETE /api/artifacts/{id}/departments/{deptId} manually link or unlink an artifact to a department. An artifact can belong to multiple departments simultaneously.",
            "Orchestrator Platform — API Reference v2.0", "api_endpoints",
            Artifacts.PlatformKnowledge),

        // Auth and tenant isolation
        new("Authentication uses JWT Bearer tokens issued by POST /api/auth/login with body { username, password, tenantId }. The token carries claims: sub (userId), tenant_id (tenantId), name (username), and role (Admin or Member). All protected endpoints require a valid token. The tenant_id claim is the sole source of tenant identity — callers cannot access a different tenant's data by any means.",
            "Orchestrator Platform — Security and Tenant Model v2.0", "auth_model",
            Artifacts.PlatformKnowledge),

        new("Tenant isolation is enforced at two levels: JWT claim verification and database query filtering. HybridRagService always includes .Where(b => b.TenantId == tenantId) before executing queries, ensuring that even a malformed token cannot cause cross-tenant data leakage at the ORM level. Integration tests explicitly verify the Search_DoesNotLeakAcrossTenants behaviour.",
            "Orchestrator Platform — Security and Tenant Model v2.0", "tenant_isolation",
            Artifacts.PlatformKnowledge),

        // User and permission model
        new("Each tenant has its own set of users stored in the TenantUsers table. Users have a Role of Admin or Member. Admin users bypass all artifact permission checks. Member users can only read or write artifacts they have been explicitly granted access to via the ArtifactPermissions table. User management is available to Admins via GET/POST /api/users and PATCH /api/users/{id}. Per-artifact permissions are managed via GET/PUT /api/users/{id}/permissions.",
            "Orchestrator Platform — User and Permission Model v1.0", "user_permissions",
            Artifacts.PlatformKnowledge),

        // Hybrid RAG
        new("The platform implements a hybrid RAG (Retrieval-Augmented Generation) pattern. 'Hybrid' refers to the combination of semantic vector search and relational filtering in a single query: pgvector handles approximate nearest-neighbour retrieval over embedding vectors, while PostgreSQL handles the tenant, artifact, and department predicates. This avoids the need for a separate vector database while retaining full SQL expressiveness for filtering.",
            "Orchestrator Platform — Architecture Overview v2.0", "rag_architecture",
            Artifacts.PlatformKnowledge),

        new("Vector similarity search uses the pgvector PostgreSQL extension. Embeddings are stored as dimensionless vector columns in the BusinessContexts table, supporting any provider output dimension (1536 for OpenAI ada-002, 768 for Ollama nomic-embed-text, etc.). Retrieval uses L2 Euclidean distance via the <-> operator. The top-K results (default K=5) are returned ordered by relevance.",
            "Orchestrator Platform — Architecture Overview v2.0", "rag_architecture",
            Artifacts.PlatformKnowledge),

        // Embedding provider
        new("The embedding provider is vendor-agnostic and configured per-tenant. Supported providers: OpenAI (text-embedding-ada-002, text-embedding-3-small, text-embedding-3-large), Azure OpenAI (same models via Azure endpoint), and Ollama (nomic-embed-text, mxbai-embed-large, all-minilm, bge-m3 for fully local inference). A system-wide default config can be set, with per-tenant overrides. The API never returns the stored API key — only a hasApiKey boolean flag.",
            "Orchestrator Platform — Embedding Configuration v2.0", "embedding_model",
            Artifacts.PlatformKnowledge),

        // AI Supervisor
        new("The AI supervisor (ArtifactSupervisorService) routes each ingested text chunk to the most semantically appropriate artifact. It reads the tenant's DepartmentManifest (if one exists) as context before making routing decisions. The supervisor can: (1) route to an existing artifact, (2) create a new department and artifact on-demand, or (3) fall back to the shared cross-cutting artifact. If no chat model is configured, it always falls back to the shared artifact.",
            "Orchestrator Platform — AI Supervisor v1.0", "ai_supervisor",
            Artifacts.PlatformKnowledge),

        new("The DepartmentManifest is a free-text blob (Markdown recommended) stored per tenant via PUT /api/tenants/{id}/department-manifest. It defines the tenant's department structure, artifact guidelines, and routing instructions for the AI supervisor. When the manifest is saved, Markdown ## headings are automatically synced to Department rows. There are no hard limits on the number of artifacts per department — the manifest expresses ideal targets, and the AI uses them as guidance.",
            "Orchestrator Platform — Department Manifest v1.0", "ai_supervisor",
            Artifacts.PlatformKnowledge),

        // Guardrail pipeline
        new("The guardrail pipeline is implemented as an IGuardrailService interface with a single EvaluateAsync method that receives the incoming query text and returns a GuardrailResult indicating whether the request should proceed, be modified, or be blocked. The current implementation is a pass-through stub that always returns Allowed. The interface is positioned in the request path so a real policy engine can be substituted without changing the API controller or RAG service.",
            "Orchestrator Platform — Guardrail Pipeline v1.0", "guardrail_pipeline",
            Artifacts.PlatformKnowledge),

        // Semantic Kernel plugin
        new("The Orchestrator platform includes a Semantic Kernel plugin named BusinessContextPlugin in the Orchestrator.Engine project. It exposes a KernelFunction named search_business_context that accepts a string query, calls IHybridRagService.SearchAsync, and returns matching context chunks as a formatted numbered list. The function is decorated with Description attributes so the SK planner can select it automatically.",
            "Orchestrator Platform — Semantic Kernel Integration v1.0", "sk_plugin",
            Artifacts.PlatformKnowledge),

        // Aspire setup
        new("The platform uses .NET Aspire for local orchestration. The Orchestrator.AppHost project defines the application model: it declares the PostgreSQL resource with the pgvector image, the API project, and their dependency wiring. Running the AppHost starts all services, applies EF Core migrations automatically, and opens the Aspire dashboard where you can inspect logs, traces, and health status for each resource.",
            "Orchestrator Platform — Developer Setup Guide v2.0", "aspire_setup",
            Artifacts.PlatformKnowledge),

        // Seed data
        new("The platform ships with two built-in demo tenants. FibreCore Networks (TenantId a1a1a1a1-...) is an incumbent with 75% market share, three departments (Commercial, Operations, Legal & Compliance), four artifacts, and 15 knowledge chunks. SwiftFibre (TenantId b2b2b2b2-...) is a challenger with 25% market share, three departments (Growth, Operations, Retention), six artifacts, and 15 knowledge chunks. Each tenant is seeded with a DepartmentManifest and has an admin user (username: admin, password: Admin123!).",
            "Orchestrator Platform — Seed Data Reference v2.0", "seed_data",
            Artifacts.PlatformKnowledge),

        // Current limitations
        new("Current platform limitations: (1) The guardrail service is a stub — all requests pass through regardless of content. A real policy engine should be wired in before production exposure. (2) There is no POST /api/generate endpoint — LLM orchestration must be handled by the caller using the Semantic Kernel BusinessContextPlugin. (3) The BusinessContextPlugin is implemented but not registered with a running Kernel instance by default.",
            "Orchestrator Platform — Known Limitations v2.0", "current_limitations",
            Artifacts.PlatformKnowledge),
    ];
}
