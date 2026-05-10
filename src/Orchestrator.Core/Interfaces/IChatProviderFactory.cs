using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;

namespace Orchestrator.Core.Interfaces;

public interface IChatProviderFactory
{
    // Returns null if ChatModelId is not configured — supervisor degrades gracefully
    IChatClient? GetChatClient(EmbeddingProviderConfig config);
}
