using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Embeddings;

/// <summary>Expose a classified embedding failure without provider payload or exception details.</summary>
/// <param name="error">Stable catalog error.</param>
/// <param name="retryable">Whether another attempt can succeed.</param>
public sealed class EmbeddingFailure(BackendError error, bool retryable = false) : Exception(error.Code)
{
    public BackendError Error { get; } = error;
    public string Code => Error.Code;
    public bool Retryable { get; } = retryable;
}
