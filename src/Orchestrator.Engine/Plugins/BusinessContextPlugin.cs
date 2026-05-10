using System.ComponentModel;
using Microsoft.SemanticKernel;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Engine.Plugins;

public sealed class BusinessContextPlugin
{
    private readonly IHybridRagService _ragService;

    public BusinessContextPlugin(IHybridRagService ragService)
    {
        _ragService = ragService;
    }

    [KernelFunction("search_business_context")]
    [Description("Search for relevant business context chunks for a given query, scoped to a specific tenant")]
    public async Task<string> SearchBusinessContextAsync(
        [Description("The search query")] string query,
        [Description("The tenant ID (GUID)")] string tenantId,
        [Description("Maximum number of results to return")] int topK = 5)
    {
        var results = await _ragService.SearchAsync(query, Guid.Parse(tenantId), topK);

        return results.Count == 0
            ? "No relevant business context found."
            : string.Join("\n---\n", results.Select(r => r.Text));
    }
}
