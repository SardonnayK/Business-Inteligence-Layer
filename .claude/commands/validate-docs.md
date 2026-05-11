# Validate Documentation

Check that all files under `dox/` are consistent with the current state of the codebase. Report every discrepancy found — missing sections, stale content, incorrect API shapes, or undocumented features.

## What to check

### 1. Entities and data model (`dox/human/data-model.md`, `dox/ai/knowledge-base-schema.md`)

Read every entity file under `src/Orchestrator.Core/Entities/` and verify:
- Every entity and its columns are documented
- Every join table (e.g. `ArtifactDepartment`) has an entry
- Foreign key relationships and cascade behaviour match the code
- No removed entities or columns are still mentioned in docs

### 2. API surface (`dox/human/api-reference.md`, `dox/ai/system-context.md`)

Read every controller under `src/Orchestrator.Api/Controllers/` and verify:
- Every `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[HttpPatch]` endpoint is documented
- Request/response shapes match the actual controller code (including new fields like `departments: [{id, name}]`)
- Auth requirements (`[Authorize]`, `[Authorize(Roles = "Admin")]`) are noted accurately
- No removed endpoints are still in the docs

### 3. Services and architecture (`dox/ai/system-context.md`, `dox/human/overview.md`)

Read `src/Orchestrator.Infrastructure/Services/ArtifactSupervisorService.cs` and `HybridRagService.cs` and verify:
- The AI supervisor routing logic is described accurately (manifest-aware, 3 actions: route/create/shared)
- The DepartmentManifest usage is documented (read at start of both routing and discovery)
- No hardcoded artifact-count rules (1/2/3) remain in docs — they were replaced by manifest guidance
- The hybrid search SQL (uses `ArtifactDepartments` join, not `Artifact.DepartmentId`) is accurate

### 4. Seed data (`dox/human/data-model.md`, `dox/ai/knowledge-base-schema.md`)

Read `src/Orchestrator.Infrastructure/Seeding/SeedData.cs` and `SystemSeedData.cs` and verify:
- Seeded tenant names and IDs match
- Department and artifact names match the seed data
- Chunk counts are correct (FibreCore: 15, SwiftFibre: 15, Platform: 13)
- Admin credentials (username: admin, password: Admin123!) are documented

### 5. Auth and permissions (`dox/human/api-reference.md`, `dox/ai/system-context.md`)

Read `src/Orchestrator.Api/Controllers/AuthController.cs` and `UserController.cs` and verify:
- JWT login flow (POST /api/auth/login body shape, response shape) is documented
- User management endpoints (GET/POST /api/users, PATCH /api/users/{id}) are documented
- Permission endpoints (GET/PUT /api/users/{id}/permissions) are documented
- The ArtifactPermission model is explained

### 6. Missing features

Check for any controllers, entities, or service features that exist in code but have no corresponding documentation entry.

## Output format

Report findings as:

```
PASS: <area> — matches code
WARN: <area> — <brief description of discrepancy>
FAIL: <area> — <missing or incorrect content>
```

End with a summary line: `X issues found (Y WARN, Z FAIL)` and a prioritised list of the top items to fix.
