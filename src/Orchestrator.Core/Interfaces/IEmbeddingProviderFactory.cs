using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;

namespace Orchestrator.Core.Interfaces;

public interface IEmbeddingProviderFactory
{
    IEmbeddingGenerator<string, Embedding<float>> GetGenerator(EmbeddingProviderConfig config);
}
