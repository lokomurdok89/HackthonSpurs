using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Embeddings;

/// <summary>Identify a compatible vector space without provider connection details.</summary>
/// <param name="IsConfigured">Whether this deployment can compute embeddings.</param>
/// <param name="ModelId">Identifier of the active model.</param>
/// <param name="ProfileVersion">Operator-managed revision of this vector space.</param>
/// <param name="VectorDimensions">Number of components in every vector.</param>
public sealed record EmbeddingProfile(bool IsConfigured, string ModelId, string ProfileVersion, int VectorDimensions);

public interface ITextEmbeddings
{
    /// <summary>Compute one validated vector per bounded input, in input order.</summary>
    /// <param name="texts">Excerpts or a single explicit search query.</param>
    /// <param name="ct">Cancellation for this operation.</param>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

/// <summary>Expose a classified embedding failure without provider payload or exception details.</summary>
/// <param name="error">Stable catalog error.</param>
/// <param name="retryable">Whether another attempt can succeed.</param>
public sealed class EmbeddingFailure(BackendError error, bool retryable = false) : Exception(error.Code)
{
    public BackendError Error { get; } = error;
    public string Code => Error.Code;
    public bool Retryable { get; } = retryable;
}
