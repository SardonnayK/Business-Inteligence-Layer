using Orchestrator.Core.Interfaces;

namespace Orchestrator.Api.Middleware;

public class GuardrailMiddleware
{
    private readonly RequestDelegate _next;

    public GuardrailMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGuardrailService guardrailService)
    {
        // Only intercept AI-generating endpoints
        if (!context.Request.Path.StartsWithSegments("/api/generate"))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(buffer).ReadToEndAsync();

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
            && Guid.TryParse(tenantHeader, out var tenantId))
        {
            var validation = await guardrailService.ValidateAsync(responseBody, tenantId);
            if (!validation.IsValid)
            {
                context.Response.Body = originalBody;
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(new { error = "Guardrail violation", reason = validation.Reason });
                return;
            }
        }

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }
}
