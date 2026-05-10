namespace Orchestrator.Infrastructure.Seeding;

internal static class SystemSeedData
{
    // ─── Orchestrator Platform (System Tenant) ────────────────────────────────
    // Self-documentation · enables the AI to answer questions about the platform
    // using its own RAG pipeline rather than relying on static prompts.

    internal static readonly Guid TenantId = new("c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3");
    internal const string TenantName = "Orchestrator Platform";

    internal static class Projects
    {
        internal static readonly Guid PlatformCapabilities = new("c3c3c3c3-0000-0000-0000-000000000010");
        internal static readonly Guid IntegrationGuide     = new("c3c3c3c3-0000-0000-0000-000000000020");
    }

    internal static readonly SeedData.ProjectSeed[] ProjectSeeds =
    [
        new(Projects.PlatformCapabilities, TenantId,
            "Platform Capabilities",
            "Documents what the Orchestrator platform can do: its API surface, RAG pipeline, embedding model, guardrail layer, Semantic Kernel plugin, and Aspire hosting setup. Kept current so the AI can answer capability questions using first-party knowledge."),
        new(Projects.IntegrationGuide, TenantId,
            "Integration Guide",
            "Documents how to connect to, run, and extend the Orchestrator platform: tenant isolation model, available endpoints, local development setup, known limitations, and the path forward for production readiness."),
    ];

    internal static readonly SeedData.RequirementSeed[] RequirementSeeds =
    [
        // Platform Capabilities
        new(new("c1000001-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The platform must expose a POST /api/ingest endpoint that accepts a text payload, a tenantId, a source label, and a category label. It must generate an embedding via the configured embedding model and persist both the raw text and the vector to the BusinessContexts table.", "Approved"),
        new(new("c1000002-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The platform must expose a POST /api/search endpoint that accepts a natural-language query and a tenantId header. It must embed the query, perform an L2 nearest-neighbour search against pgvector filtered to that tenant, and return the top-K matching chunks with similarity scores.", "Approved"),
        new(new("c1000003-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The platform must expose a POST /api/seed endpoint that triggers the DataSeeder, which creates tenants, projects, requirements, and business-context embeddings for all registered seed datasets. The seeder must be idempotent: re-running must not create duplicates.", "Approved"),
        new(new("c1000004-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The guardrail pipeline must intercept every inbound request before it reaches the RAG search path. In the current stub implementation it passes all requests through unconditionally. The interface is defined so that a real policy engine (toxicity filter, PII redactor, rate limiter) can be wired in without changing downstream code.", "In Review"),
        new(new("c1000005-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "The BusinessContextPlugin Semantic Kernel plugin must expose a search_business_context KernelFunction that accepts a query string, calls the hybrid RAG service, and returns a formatted string of matching context chunks. The plugin class is implemented but not yet registered with a Kernel instance.", "Draft"),
        new(new("c1000006-0000-0000-0000-000000000000"), Projects.PlatformCapabilities,
            "All services must expose a /health/live and /health/ready endpoint following the Aspire ServiceDefaults pattern. The Aspire dashboard must aggregate health status across the API, AppHost, and dependent services.", "Approved"),

        // Integration Guide
        new(new("c2000001-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "Every API call that reads or writes tenant data must include an X-Tenant-Id header carrying a valid tenant GUID. The platform performs no authentication today; the header is trusted as-is. Production deployments must add an authentication middleware layer before exposing the API externally.", "Approved"),
        new(new("c2000002-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The local development environment can be started in three ways: (1) via the Aspire AppHost project which orchestrates all dependencies, (2) via docker-compose which starts Postgres with pgvector and the API, or (3) via the test.ps1 PowerShell script which runs integration tests against a live stack.", "Approved"),
        new(new("c2000003-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "New tenants must be registered in the Tenants table before any ingest or search calls are made for them. The POST /api/seed endpoint seeds the built-in demo tenants automatically. Custom tenants must be inserted via direct database access or a future tenant-management API.", "In Review"),
        new(new("c2000004-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The platform does not currently expose a POST /api/generate endpoint. Text generation must be invoked by the caller using the Semantic Kernel BusinessContextPlugin together with a separately provisioned Azure OpenAI or OpenAI Completions endpoint. This separation is intentional to keep the BI layer stateless with respect to LLM orchestration.", "Draft"),
        new(new("c2000005-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "Extending the seed dataset requires adding a new static class to Orchestrator.Infrastructure/Seeding/ following the SeedData.cs or SystemSeedData.cs pattern, then registering it with a new SeedCompanyAsync call in DataSeeder.cs. No schema migrations are needed as all seed data fits the existing entity model.", "Approved"),
        new(new("c2000006-0000-0000-0000-000000000000"), Projects.IntegrationGuide,
            "The pgvector extension must be enabled in Postgres before the application starts. The AppDbContext applies the extension and creates the BusinessContexts vector index via EF Core migrations using the Npgsql.EntityFrameworkCore.PostgreSQL.VectorSearch package.", "Approved"),
    ];

    internal static readonly SeedData.ContextChunkSeed[] ContextChunks =
    [
        // API endpoints
        new("The Orchestrator platform exposes three primary API endpoints. POST /api/ingest accepts { text, tenantId, source, category } and stores the text with its embedding vector in the BusinessContexts table. POST /api/search accepts a query body and an X-Tenant-Id header and returns the top-K semantically similar chunks for that tenant. POST /api/seed triggers the DataSeeder to populate all registered demo tenants idempotently. GET /health/live and GET /health/ready return liveness and readiness status respectively.",
            "Orchestrator Platform — API Reference v1.0", "api_endpoints"),

        new("The POST /api/search endpoint performs a hybrid retrieval: it first filters the BusinessContexts table by TenantId using a standard SQL WHERE clause (relational filter), then ranks the filtered rows by L2 Euclidean distance between the query embedding and the stored vector using pgvector's <-> operator. The top-K results (default K=5) are returned with their distance scores. Lower distance means higher relevance.",
            "Orchestrator Platform — API Reference v1.0", "api_endpoints"),

        // Tenant isolation
        new("Tenant isolation is enforced via the X-Tenant-Id HTTP request header. Every search and ingest call must include this header set to a valid tenant GUID. The platform does not perform authentication or authorisation today — the header is trusted without verification. All data queries include a hard TenantId filter so one tenant cannot read another tenant's business context, requirements, or projects.",
            "Orchestrator Platform — Security and Tenant Model v1.0", "tenant_isolation"),

        new("The Tenants, Projects, Requirements, and BusinessContexts tables all carry a TenantId foreign key. EF Core queries in HybridRagService always include .Where(b => b.TenantId == tenantId) before executing against the database, ensuring that even if a caller supplies an incorrect header, the ORM-level filter prevents cross-tenant data leakage at the query layer.",
            "Orchestrator Platform — Security and Tenant Model v1.0", "tenant_isolation"),

        // Hybrid RAG approach
        new("The platform implements a hybrid RAG (Retrieval-Augmented Generation) pattern. 'Hybrid' refers to the combination of semantic vector search and relational filtering in a single query: pgvector handles approximate nearest-neighbour retrieval over embedding vectors, while PostgreSQL handles the tenant and metadata predicates. This avoids the need for a separate vector database while retaining full SQL expressiveness for filtering.",
            "Orchestrator Platform — Architecture Overview v1.0", "rag_architecture"),

        new("Vector similarity search is performed using the pgvector PostgreSQL extension. Embeddings are stored as vector(1536) columns in the BusinessContexts table. Retrieval uses L2 (Euclidean) distance via the <-> operator. An IVFFlat index is created on the embedding column to accelerate approximate nearest-neighbour queries at scale. The index is built automatically during EF Core migration.",
            "Orchestrator Platform — Architecture Overview v1.0", "rag_architecture"),

        // Embedding model
        new("All text embeddings are generated using OpenAI's text-embedding-ada-002 model, which produces 1536-dimensional dense float vectors. The embedding client is configured via the OPENAI_API_KEY environment variable and the model name is fixed in the embedding service configuration. Switching to a different model requires a full re-embedding of all stored BusinessContext chunks because vector dimensions must be consistent within a table.",
            "Orchestrator Platform — Embedding Configuration v1.0", "embedding_model"),

        new("The embedding service wraps the Azure OpenAI or OpenAI SDK and is registered as a singleton in the DI container. It is consumed by HybridRagService for both ingest (embed the incoming text) and search (embed the incoming query). Embedding calls are made synchronously within the request pipeline; there is no batch queue or background embedding worker at this time.",
            "Orchestrator Platform — Embedding Configuration v1.0", "embedding_model"),

        // Guardrail pipeline
        new("The guardrail pipeline is implemented as an IGuardrailService interface with a single EvaluateAsync method that receives the incoming query text and returns a GuardrailResult indicating whether the request should proceed, be modified, or be blocked. The current implementation is a pass-through stub that always returns Allowed. The interface is positioned in the request path so a real policy engine can be substituted without changing the API controller or RAG service.",
            "Orchestrator Platform — Guardrail Pipeline v1.0", "guardrail_pipeline"),

        new("Future guardrail implementations may include: prompt injection detection, PII redaction before text is stored or returned, toxicity classification, per-tenant rate limiting, and query length enforcement. The stub is intentionally left in place as scaffolding to signal the intended extension point to integrators. Any production deployment should replace the stub before external exposure.",
            "Orchestrator Platform — Guardrail Pipeline v1.0", "guardrail_pipeline"),

        // Semantic Kernel plugin
        new("The Orchestrator platform includes a Semantic Kernel plugin named BusinessContextPlugin located in Orchestrator.Infrastructure/Plugins/. It exposes a KernelFunction named search_business_context that accepts a string query parameter, calls IHybridRagService.SearchAsync, and returns the matching context chunks formatted as a numbered list. The function is decorated with Description attributes so the SK planner can select it automatically.",
            "Orchestrator Platform — Semantic Kernel Integration v1.0", "sk_plugin"),

        new("The BusinessContextPlugin is implemented and tested in isolation but is not yet registered with a running Kernel instance. To wire it up, the caller must create a Kernel, add the plugin via kernel.Plugins.AddFromObject(new BusinessContextPlugin(ragService)), and then invoke it through the planner or directly. A future /api/generate endpoint will handle this orchestration server-side.",
            "Orchestrator Platform — Semantic Kernel Integration v1.0", "sk_plugin"),

        // Aspire setup
        new("The platform uses .NET Aspire for local orchestration. The Orchestrator.AppHost project defines the application model: it declares the PostgreSQL resource with the pgvector image, the API project, and their dependency wiring. Running the AppHost starts all services and opens the Aspire dashboard at https://localhost:15888, where you can inspect logs, traces, and health status for each resource.",
            "Orchestrator Platform — Developer Setup Guide v1.0", "aspire_setup"),

        new("Aspire ServiceDefaults are applied to the API project via AddServiceDefaults() in Program.cs. This automatically configures OpenTelemetry tracing and metrics, health check endpoints at /health/live and /health/ready, and resilience policies on outbound HTTP clients. The ServiceDefaults package is maintained in the Orchestrator.ServiceDefaults project and shared across all service projects in the solution.",
            "Orchestrator Platform — Developer Setup Guide v1.0", "aspire_setup"),

        // Seed data structure
        new("The platform ships with two built-in demo tenants representing competing telecom companies. FibreCore Networks (TenantId a1a1a1a1-...) is an incumbent with 75% market share, premium pricing, and urban-first rollout. SwiftFibre (TenantId b2b2b2b2-...) is a challenger with 25% market share, value pricing, and suburban-first expansion. Each tenant has three projects, 13–15 requirements, and 15 business-context chunks covering pricing, rollout, SLAs, and competitive strategy.",
            "Orchestrator Platform — Seed Data Reference v1.0", "seed_data"),

        new("Each seed dataset follows the same shape: a static TenantId GUID, a Projects inner class with named Guid constants, and three arrays — ProjectSeeds (ProjectSeed records), RequirementSeeds (RequirementSeed records), and ContextChunks (ContextChunkSeed records). The DataSeeder iterates these arrays and calls SeedCompanyAsync, which is fully idempotent: it checks for existence before inserting and skips embedding ingestion if the chunk count already matches.",
            "Orchestrator Platform — Seed Data Reference v1.0", "seed_data"),

        // How to run
        new("There are three ways to run the Orchestrator platform locally. (1) Aspire: open the solution in Visual Studio or Rider, set Orchestrator.AppHost as the startup project, and press Run. (2) Docker Compose: run 'docker-compose up' from the repository root to start Postgres and the API together. (3) test.ps1: run the PowerShell script from the repository root to spin up the stack and execute the full integration test suite, then tear down.",
            "Orchestrator Platform — Developer Setup Guide v1.0", "how_to_run"),

        // Current limitations
        new("Current platform limitations as of the initial release: (1) No authentication — the X-Tenant-Id header is trusted without verification; any caller can impersonate any tenant. (2) The guardrail service is a stub — all requests pass through regardless of content. (3) There is no POST /api/generate endpoint — LLM orchestration must be handled by the caller. (4) The BusinessContextPlugin is implemented but not registered with a Kernel, so it cannot be invoked via the SK planner without additional wiring. These are known gaps, not bugs, and are tracked as Draft requirements for the next development phase.",
            "Orchestrator Platform — Known Limitations v1.0", "current_limitations"),
    ];
}
