using HackthonSpurs.Application.Embeddings;

namespace HackthonSpurs.Application.Processing;

/// <summary>Describe ownership of one index attempt and its persisted OCR source.</summary>
/// <param name="Id">Opaque receipt being indexed.</param>
/// <param name="Token">Current lease ownership token.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="Reading">Stored result; no original bytes.</param>
public sealed record IndexClaim(Guid Id, Guid Token, int Attempt, string Reading);

public interface IIndexJobs
{
    /// <summary>Try to acquire one eligible OCR index lease.</summary>
    /// <param name="ct">Worker shutdown token.</param>
    Task<IndexClaim?> ClaimAsync(CancellationToken ct);

    /// <summary>Apply a failure or retry only if this attempt still owns the lease.</summary>
    /// <param name="claim">Lease authorizing the write.</param>
    /// <param name="code">Safe error identifier.</param>
    /// <param name="retryDelay">Backoff before another attempt, or null for a terminal failure.</param>
    /// <param name="ct">Worker shutdown token.</param>
    Task<bool> FailAsync(IndexClaim claim, string code, TimeSpan? retryDelay, CancellationToken ct);

    /// <summary>Replace excerpts and complete the job atomically under the unexpired lease.</summary>
    /// <param name="claim">Lease authorizing publication.</param>
    /// <param name="chunks">Validated OCR excerpts in source order.</param>
    /// <param name="vectors">Validated vectors in the corresponding order.</param>
    /// <param name="profile">Model metadata for the vectors.</param>
    /// <param name="ct">Worker shutdown token.</param>
    Task<bool> PublishAsync(IndexClaim claim, IReadOnlyList<OcrIndexChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors, EmbeddingProfile profile, CancellationToken ct);
}
