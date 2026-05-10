# Project Specification: AI-Orchestrated SDLC Platform (v2)
## Target Architecture
A multi-tenant, agentic orchestrator. This system uses a Hybrid Storage approach:
- **Relational Data:** PostgreSQL (Tenants, Users, Workflow State, Exact Metadata).
- **Vector Data:** pgvector (Business context, semantic search, unstructured documentation).
- **Orchestration:** Semantic Kernel.

## Tech Stack
- **Runtime:** .NET 10
- **Database:** PostgreSQL with pgvector extension (Single source of truth for both relational and vector data).
- **AI Integration:** Microsoft.SemanticKernel.
- **Containerization:** Docker & Docker Compose.
- **Configuration:** .env files for secrets and connection strings.

## Project Structure (Clean Architecture)
1. **Orchestrator.Api**: ASP.NET Core Web API.
2. **Orchestrator.Core**: Domain entities (Tenant, Project, Requirement), Interfaces, and Policy-as-Code definitions.
3. **Orchestrator.Engine**: Semantic Kernel Plugins, Agentic Logic, Prompt Templates.
4. **Orchestrator.Infrastructure**: Entity Framework Core (Postgres), Vector Search implementation, External API Clients.
5. **Orchestrator.Tests**: Integration tests using TestContainers.

## Initial Setup Requirements
1.  **Docker Orchestration**: Create a `docker-compose.yml` including:
    -   `app`: The .NET API.
    -   `db`: PostgreSQL with `pgvector` image.
2.  **Environment Management**: Use a `.env` file for `ConnectionStrings`, `OpenAI_ApiKey`, and `Database_Password`. Ensure the API reads these during startup.
3.  **Hybrid RAG Service**: Implement a service that can:
    -   Store business context (text + metadata) into Postgres.
    -   Perform a vector search filtered by `Tenant-Id` (Relational + Vector hybrid query).
4.  **Guardrail Pipeline**: A middleware or filter that validates AI output against business rules stored in the Relational DB.

## Claude Code Instructions
1. Initialize a new .NET 10 solution and create the 5 projects.
2. Create a `docker-compose.yml` and a template `.env` file.
3. Set up EF Core with pgvector support (use `Pgvector.EntityFrameworkCore` NuGet).
4. Implement a "Business Context Ingestion" endpoint that takes text, generates embeddings, and saves them to the tenant-scoped table.
