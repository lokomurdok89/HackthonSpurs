namespace HackthonSpurs.Application.Processing;

/// <summary>Describe lease ownership and the expected original for one reading attempt.</summary>
/// <param name="Id">Opaque document receipt.</param>
/// <param name="Token">Current lease ownership token.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="ByteCount">Expected original length.</param>
/// <param name="MediaType">Verified media type.</param>
/// <param name="Hash">Expected SHA-256 of the original.</param>
/// <param name="SendToOcr">Whether this is the explicit external OCR lane.</param>
public sealed record ReadingClaim(Guid Id, Guid Token, int Attempt, long ByteCount, string MediaType, string Hash, bool SendToOcr);

/// <summary>Carry the validated reading or a classified failure from the reader adapter.</summary>
/// <param name="Json">Validated public reading on success.</param>
/// <param name="Error">Safe reader failure code.</param>
/// <param name="Retryable">Whether the reported failure admits another attempt.</param>
public sealed record ReadOutcome(string? Json = null, string? Error = null, bool Retryable = false);

/// <summary>Carry the decided reading publication, terminal failure, or retry.</summary>
/// <param name="Json">Validated reading on success.</param>
/// <param name="Error">Safe terminal error code; omitted for a retry.</param>
/// <param name="RetryDelay">Delay before the next claim, if permitted.</param>
public sealed record ReadingCompletion(string? Json, string? Error, TimeSpan? RetryDelay);

public interface IReadingJobs
{
    /// <summary>Try to acquire a lease for one eligible document in the active lane.</summary>
    /// <param name="ct">Worker shutdown token.</param>
    Task<ReadingClaim?> ClaimAsync(CancellationToken ct);

    /// <summary>Apply the completion atomically, returning false if the attempt no longer owns the lease.</summary>
    /// <param name="claim">Lease authorizing this attempt.</param>
    /// <param name="completion">Publication, failure or retry to persist.</param>
    /// <param name="ct">Worker shutdown token.</param>
    Task<bool> FinishAsync(ReadingClaim claim, ReadingCompletion completion, CancellationToken ct);
}

public interface IDocumentReader
{
    /// <summary>Read the expected original and return validated evidence or a classified failure.</summary>
    /// <param name="claim">Lease metadata identifying this attempt.</param>
    /// <param name="stopping">Worker shutdown token.</param>
    Task<ReadOutcome> ReadAsync(ReadingClaim claim, CancellationToken stopping);
}
