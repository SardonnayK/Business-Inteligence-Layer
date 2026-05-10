# API Reference

## General conventions

**Base URL (Aspire):** `http://localhost:<port>` — the exact port is shown in the Aspire dashboard next to the `api` resource. It is randomised on each run unless you pin it.

**Base URL (docker-compose):** `http://localhost:8080`

**OpenAPI schema:** `GET /openapi/v1.json` — only served when `ASPNETCORE_ENVIRONMENT=Development`.

**Tenant identification:** Every business-context endpoint requires an `X-Tenant-Id` header containing a GUID. This is the primary isolation boundary. Requests without a valid tenant ID that find no matching tenant will get an empty result set (search) or a 500 (ingest, if the tenant row doesn't exist in the DB).

**Content-Type:** All POST bodies are `application/json`. All responses are `application/json`.

---

## Business Context endpoints

### POST /api/business-context

Ingest a text chunk into a tenant's knowledge base. The system calls OpenAI's `text-embedding-ada-002` model to generate a 1 536-dimension vector, then persists both the original text and the vector together.

**Headers**

| Header | Required | Description |
|---|---|---|
| `X-Tenant-Id` | Yes | The tenant's GUID |
| `Content-Type` | Yes | `application/json` |

**Request body**

```json
{
  "text": "string (required) — the text to embed and store",
  "source": "string or null — a label identifying where this text came from (e.g. a document name or URL)",
  "category": "string or null — a topic label for filtering or display purposes"
}
```

**Example request**

```bash
curl -s -X POST \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1" \
  -d '{
    "text": "FibreCore does not match competitor pricing. Our response to a price challenge is a retention campaign, not a rate card change.",
    "source": "Competitive Response Playbook v1.1",
    "category": "competitive_response"
  }' \
  http://localhost:8080/api/business-context
```

**Example response (200 OK)**

```json
{
  "id": "3f7c2a1e-0000-0000-0000-000000000000",
  "tenantId": "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1",
  "createdAt": "2026-05-10T09:14:22.341Z"
}
```

The embedding vector is stored in the database but is never returned in the API response. Only the metadata is returned.

**Notes**
- Each call makes one API request to OpenAI. If the OpenAI key is missing or invalid the request will fail.
- There is no deduplication. Calling with the same text twice creates two rows.

---

### GET /api/business-context/search

Search a tenant's knowledge base using a natural-language query. The query is embedded using the same model as ingest, and the results are ranked by L2 (Euclidean) vector distance — closest embedding first. Results are always scoped to the tenant in the header; no other tenant's data is ever returned.

**Headers**

| Header | Required | Description |
|---|---|---|
| `X-Tenant-Id` | Yes | The tenant's GUID |

**Query parameters**

| Parameter | Required | Default | Description |
|---|---|---|---|
| `query` | Yes | — | The natural-language search query |
| `topK` | No | `5` | Maximum number of results to return |

**Example request**

```bash
curl -s \
  -H "X-Tenant-Id: b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2" \
  "http://localhost:8080/api/business-context/search?query=rural+broadband+commitments&topK=2"
```

**Example response (200 OK)**

```json
[
  {
    "id": "1a2b3c4d-0000-0000-0000-000000000001",
    "text": "SwiftFibre is committed to bringing genuine gigabit fibre to rural and semi-rural communities overlooked by larger providers. Our rural coverage commitment: any node we build in a rural area launches at a minimum of 100Mbps symmetric, with a roadmap targeting full 1Gbps symmetrical availability across all rural sites by end of 2026.",
    "source": "SwiftFibre Rural Coverage Commitment — Public Statement",
    "category": "rural_coverage",
    "createdAt": "2026-05-10T08:00:00.000Z"
  },
  {
    "id": "1a2b3c4d-0000-0000-0000-000000000002",
    "text": "We know rural customers are sceptical — they have been promised fast broadband before and let down. That is why our rural rollout includes a 90-day performance guarantee: if a rural customer does not receive at least 80% of their advertised speed during the first 90 days, they can exit with no penalty and keep the router.",
    "source": "SwiftFibre Rural Coverage Commitment — Public Statement",
    "category": "rural_coverage",
    "createdAt": "2026-05-10T08:00:00.000Z"
  }
]
```

**Notes**
- An empty array `[]` is a valid response when no chunks exist for the tenant, or when topK results cannot be found.
- The ranking is purely by vector similarity. There is no BM25 or keyword component in the current implementation.
- Each call makes one API request to OpenAI to embed the query.

---

## Development-only endpoints

These endpoints are only registered when `ASPNETCORE_ENVIRONMENT=Development`. They return `404 Not Found` in production.

### POST /api/dev/seed

Populates the database with two tenants (FibreCore Networks and SwiftFibre), their projects, requirements, and embedded business context chunks. Safe to call multiple times — each entity is checked for existence before insertion, and context chunks are skipped if the count already matches.

**No headers or request body required.**

**Example request**

```bash
curl -s -X POST http://localhost:8080/api/dev/seed
```

**Example response (200 OK) — first run**

```json
{
  "tenantsCreated": 2,
  "projectsCreated": 6,
  "requirementsCreated": 28,
  "contextChunksIngested": 30
}
```

**Example response (200 OK) — subsequent runs (all data already exists)**

```json
{
  "tenantsCreated": 0,
  "projectsCreated": 0,
  "requirementsCreated": 0,
  "contextChunksIngested": 0
}
```

**Notes**
- This makes 30 OpenAI embedding API calls (15 per tenant). It takes several seconds and uses a small amount of API credit.
- The seeded tenant GUIDs are fixed and documented in the data model reference.
- This endpoint exists purely to make local development and demonstrations self-contained. It would not be appropriate to expose in any shared or production environment.

---

## Health endpoints

These are registered by `Orchestrator.ServiceDefaults` and are always available regardless of environment.

### GET /health

Reports whether the application and all its registered health checks are healthy. The database connection is monitored automatically via the Npgsql Aspire integration.

**Example request**

```bash
curl -s http://localhost:8080/health
```

**Example response — healthy**

```
Healthy
```

**Example response — unhealthy (database unreachable)**

```
Unhealthy
```

The response body is a plain string, not JSON.

### GET /alive

A liveness probe — confirms the process is running and responding to HTTP. Does not check downstream dependencies. Suitable for use as a container liveness probe where you only care whether the process is up.

```bash
curl -s http://localhost:8080/alive
```

Returns `Healthy` when the process is running.

---

## The guardrail pipeline — important note

`GuardrailMiddleware` is registered in the request pipeline. It intercepts any request whose path starts with `/api/generate` and buffers the response body before validating it against the guardrail service.

**However: no controllers or endpoints exist at `/api/generate/*` in the current codebase.**

This means the middleware is correctly wired and will fire when those endpoints are built, but it never fires today because there is nothing to intercept. If you send a request to `/api/generate/anything` right now, you will receive a `404 Not Found` from the ASP.NET Core routing layer — the middleware sees a 404 response, and since it passes through without violation, the 404 is returned to the caller unchanged.

When `/api/generate/*` endpoints are added in future, the guardrail flow will be:

1. Request hits the API.
2. Middleware detects the `/api/generate` prefix.
3. Middleware replaces the response stream with an in-memory buffer.
4. The controller runs and writes its AI-generated content to the buffer.
5. Middleware reads the buffer and calls `IGuardrailService.ValidateAsync`.
6. If the tenant is not found or is inactive → **422 Unprocessable Entity** with `{ "error": "Guardrail violation", "reason": "..." }`.
7. If validation passes (currently: tenant exists and is active) → the buffered response is flushed to the real response stream and returned normally.

The `GuardrailService.ValidateAsync` implementation currently only checks tenant existence and the `IsActive` flag. The comment in the code explicitly marks the policy evaluation logic as an extension point for future rule loading.

---

## Error responses

| Scenario | Status | Body |
|---|---|---|
| Guardrail violation (future, when `/api/generate` exists) | 422 | `{"error":"Guardrail violation","reason":"<message>"}` |
| Missing route | 404 | ASP.NET Core default problem details |
| Unhandled exception | 500 | ASP.NET Core default problem details |

There is no authentication or authorisation middleware in the current codebase. The `X-Tenant-Id` header is not validated as a bearer token or signature — it is read as a plain GUID. Any caller who knows a tenant's GUID can query that tenant's data.
