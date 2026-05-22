using System.Net.Http.Json;
using System.Text.Json;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Agents;

public class HttpPluginAgent : IAgent
{
    private readonly AgentRegistration _registration;
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpPluginAgent(AgentRegistration registration, IHttpClientFactory httpClientFactory)
    {
        _registration = registration;
        _httpClientFactory = httpClientFactory;
    }

    public AgentCapability Capability => _registration.Capability;
    public IReadOnlyList<string> RequiredContext => [];
    public string? InputSchema => _registration.InputSchemaJson;

    public async Task<AgentResult> ExecuteAsync(AgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_registration.HttpEndpoint))
        {
            return Failure("HTTP plugin agent has no endpoint configured.");
        }

        var payload = new
        {
            executionId = request.ExecutionId,
            tenantId = request.TenantId,
            requestType = request.RequestType.ToString(),
            text = request.TextInput,
            fileName = request.FileName,
            fileBase64 = request.FileBytes is not null ? Convert.ToBase64String(request.FileBytes) : null,
            mimeType = request.MimeType,
            hintArtifactId = request.HintArtifactId,
            context = new
            {
                artifacts = request.Context.Artifacts.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    isShared = a.IsShared
                }),
                departments = request.Context.Departments.Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    description = d.Description
                }),
                manifest = request.Context.DepartmentManifest,
                ragResults = request.Context.RagResults.Select(r => new
                {
                    id = r.Id,
                    text = r.Text,
                    source = r.Source,
                    artifactId = r.ArtifactId
                })
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(_registration.HttpEndpoint, payload, cts.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentResult>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cts.Token);

            return result ?? Failure("HTTP plugin agent returned an empty response.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("HTTP plugin agent call timed out after 30 seconds.");
        }
        catch (Exception ex)
        {
            return Failure($"HTTP plugin agent call failed: {ex.Message}");
        }
    }

    private static AgentResult Failure(string message) =>
        new(
            Success: false,
            Output: message,
            RoutedArtifactId: null,
            RequiresUserConfirmation: false,
            ConfirmationMessage: null,
            SuggestedArtifactId: null,
            IngestItems: []);
}
