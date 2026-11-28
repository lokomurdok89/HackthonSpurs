namespace HackthonSpurs.Application.Processing;

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
