# Overview

## What this system is and why it exists

The Business Intelligence Layer is an AI orchestration backend. Its job is to let multiple organisations ("tenants") each store their own private knowledge base, and then let AI-powered features query that knowledge base in a way that is scoped strictly to that organisation and validated by configurable guardrails before any generated content leaves the system.

In plain terms: each tenant pushes text into the system (pricing policies, sales strategies, operational rules, regulatory constraints, etc.). The system converts that text into numerical vector embeddings using OpenAI's `text-embedding-ada-002` model and stores them in a PostgreSQL database alongside the original text. When something wants to look up relevant context — for example, to answer a question or generate a document — it sends a natural-language query, the system finds the most semantically similar chunks from that tenant's data, and returns them. If AI-generated text eventually flows through the system, a guardrail pipeline intercepts it and can reject it before it reaches the caller.

The design is clean-architecture: a thin domain layer, a concrete infrastructure layer, a Semantic Kernel plugin layer, and an ASP.NET Core API on top.

---

## The two-competitor scenario (FibreCore vs SwiftFibre)

The seeded demonstration data imagines two competing broadband companies that are both using this platform simultaneously. This is the clearest way to see the multi-tenant isolation in practice.

**FibreCore Networks** is the incumbent. It holds 75 % of the market, focuses on dense metropolitan areas, prices at a premium, and is methodical and process-heavy. Its internal strategy documents emphasise margin protection, selective discounting that needs committee approval, long enterprise contracts, and a measured competitive-response posture. When a rival gains 5 % market share over two quarters, the response is a formal Executive Committee review — not an immediate price cut.

**SwiftFibre** is the challenger. It holds 25 % of the market and is growing aggressively. It prices its plans at least 20 % below FibreCore's equivalent tier, targets suburban and rural postcodes that FibreCore has deprioritised, and moves fast — its stated goal is to go from permit approval to a street cluster being live in under 45 days. Its strategy documents openly name FibreCore as the problem they exist to solve.

Both companies have:
- A `Tenant` record in the database with a stable GUID
- Several `Project` records representing active workstreams
- `Requirement` records attached to those projects, describing system and business rules the software must enforce
- A set of `BusinessContext` chunks — the actual natural-language knowledge base — each embedded and stored as a 1 536-dimension vector

When you query the system for FibreCore's pricing strategy, you get FibreCore's internal documents. When you query for SwiftFibre's pricing strategy with SwiftFibre's tenant ID, you get a completely different set of documents. The two tenants share the same database tables but are completely isolated at query time because every query is filtered by `TenantId` before the vector similarity ranking is applied.

This isolation also extends to the guardrail: when AI output is validated, the service looks up the tenant to verify it exists and is active before any policy evaluation runs.

---

## What you can actually do with it today vs what is scaffolded

### Working today

**Ingest text into a tenant's knowledge base.** Send a POST request to `/api/business-context` with an `X-Tenant-Id` header and a JSON body containing the text, an optional source label, and an optional category label. The system calls OpenAI to generate an embedding, stores both the text and the vector in PostgreSQL, and returns the new record's ID.

**Search a tenant's knowledge base semantically.** Send a GET request to `/api/business-context/search` with the same header and a `query` string parameter. The system embeds the query, then runs a combined PostgreSQL filter (`WHERE TenantId = ?`) and vector distance sort (`ORDER BY embedding <-> query_vector`) to return the top-K most relevant chunks. This is the hybrid RAG query at the heart of the system.

**Seed the demonstration data.** In Development mode, a POST to `/api/dev/seed` inserts both FibreCore and SwiftFibre with all their projects, requirements, and embedded context chunks. The operation is idempotent — calling it twice does not create duplicates.

**Health checks.** `/health` and `/alive` are provided by the Aspire service defaults and work out of the box.

**OpenAPI schema.** In Development mode, `/openapi/v1.json` is served.

**Integration tests.** A test project spins up a real pgvector container via Testcontainers and verifies that ingest persists correctly, that searches return tenant-scoped results in similarity order, and that searches do not leak data across tenants.

### Scaffolded but not yet functional

**The guardrail pipeline.** `GuardrailMiddleware` is wired and running. It intercepts every request whose path begins with `/api/generate`. However, no controller exists under `/api/generate`. This means the middleware is in place and correct, but it never fires in the current codebase because there are no endpoints for it to intercept. The guardrail service itself only checks tenant existence and active status — the comment in the code explicitly marks the policy-evaluation step as an extension point.

**The Semantic Kernel plugin.** `BusinessContextPlugin` in the `Orchestrator.Engine` project wraps the RAG search as a Semantic Kernel `[KernelFunction]`, ready to be registered with a kernel and used in an AI agent pipeline. No controller or endpoint currently invokes this plugin.

In short: the storage, embedding, retrieval, and isolation foundations are real and working. The AI generation and guardrail enforcement layers are correctly designed and partially implemented, but there are no endpoints to drive them yet.

---

## The architecture in plain language

The system has seven projects divided into two groups.

### The application layer (Clean Architecture)

**Orchestrator.Core** contains the domain — the entity classes (`Tenant`, `Project`, `Requirement`, `BusinessContext`) and the service interfaces (`IHybridRagService`, `IGuardrailService`). Nothing in Core depends on anything external. It does not know about databases, HTTP, or OpenAI.

**Orchestrator.Infrastructure** is where the real work happens. It holds the Entity Framework Core database context, the migration history, the `HybridRagService` implementation (which actually calls OpenAI and queries PostgreSQL with pgvector), the `GuardrailService` implementation, and the `DataSeeder`. It depends on Core, PostgreSQL (via Npgsql), pgvector, and the `Microsoft.Extensions.AI` embedding abstraction.

**Orchestrator.Engine** contains the Semantic Kernel plugin. It sits between Infrastructure and the API, giving the RAG search capability an interface that a Semantic Kernel agent can discover and call automatically as a tool. It depends on Core (and therefore indirectly on Infrastructure at runtime).

**Orchestrator.Api** is the HTTP layer — ASP.NET Core controllers, the guardrail middleware, and the application entry point. It depends on Infrastructure and Engine.

**Orchestrator.Tests** is the xUnit integration test project. It uses Testcontainers to start a real pgvector container for each test run.

### The infrastructure layer (Aspire)

**Orchestrator.AppHost** is the .NET Aspire orchestrator. When you run it during development, it starts a pgvector Docker container, starts the API project, wires the database connection string from the container into the API automatically, and opens the Aspire developer dashboard. This is the recommended way to run locally.

**Orchestrator.ServiceDefaults** is a shared library used by both the AppHost and the API. It configures OpenTelemetry (traces, metrics, and structured logs), health check endpoints, service discovery, and HTTP resilience policies. Both projects call `builder.AddServiceDefaults()` to get all of this for free.

### How data flows

A request arrives at the API. If it hits `/api/generate/*`, the guardrail middleware buffers the response before returning it. The controller calls the service. The service calls OpenAI's embedding API to convert text to a vector. The vector and the text are stored in PostgreSQL together, or the vector is used to rank existing rows by L2 distance and the top results are returned. The Semantic Kernel plugin gives an agent-friendly name and description to that search capability so a future AI agent can invoke it as a tool automatically.

Telemetry, health checks, and retries are all handled by the ServiceDefaults library before any of your code runs.
