using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Services;

public class GuardrailService : IGuardrailService
{
    private readonly AppDbContext _db;

    public GuardrailService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GuardrailResult> ValidateAsync(string aiOutput, Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct);

        if (tenant is null)
            return new GuardrailResult(false, "Tenant not found");

        if (!tenant.IsActive)
            return new GuardrailResult(false, "Tenant is inactive");

        // Extension point: load policy-as-code rules from relational DB and evaluate
        return new GuardrailResult(true);
    }
}
