using Microsoft.Extensions.AI;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Returns deterministic fixed-dimension vectors without calling any external API.
/// Each unique input string gets a stable unit vector so similarity ordering is testable.
/// </summary>
internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public static readonly int Dimensions = 1536;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(v =>
        {
            var vector = BuildVector(v);
            return new Embedding<float>(vector);
        });

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    public EmbeddingGeneratorMetadata Metadata => new("fake", null, null, Dimensions);

    public object? GetService(Type serviceType, object? key = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }

    // Produces a unit vector seeded from the input string's hash.
    // Strings that share a prefix will be close in vector space.
    private static float[] BuildVector(string input)
    {
        var vector = new float[Dimensions];
        var hash = Math.Abs(input.GetHashCode());

        for (var i = 0; i < Dimensions; i++)
        {
            var angle = (hash + i) * 0.001f;
            vector[i] = MathF.Sin(angle);
        }

        // Normalise to unit length
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        for (var i = 0; i < Dimensions; i++)
            vector[i] /= magnitude;

        return vector;
    }
}
