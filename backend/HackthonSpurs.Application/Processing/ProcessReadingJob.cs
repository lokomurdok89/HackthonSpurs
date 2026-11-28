using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Processing;

/// <summary>Provide a safe operational summary of one processed lease.</summary>
/// <param name="Id">Opaque receipt.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="Outcome">Stable operational label or error code.</param>
public sealed record JobAttemptReport(Guid Id, int Attempt, string Outcome);

/// <summary>Execute one reading attempt without polling, hosting or storage-specific operations.</summary>
/// <param name="jobs">Lease-aware job port for the active lane.</param>
/// <param name="reader">Original-reading adapter for that lane.</param>
public sealed class ProcessReadingJob(IReadingJobs jobs, IDocumentReader reader)
{
    /// <summary>Claim, read and finish one eligible job; return null when no work was claimed.</summary>
    /// <param name="stopping">Worker shutdown token.</param>
    public async Task<JobAttemptReport?> RunOnceAsync(CancellationToken stopping)
    {
        var claim = await jobs.ClaimAsync(stopping);
        if (claim is null) return null;
        var outcome = await reader.ReadAsync(claim, stopping);
        var retry = RetrySchedule.AfterFailure(!claim.SendToOcr, outcome.Retryable, claim.Attempt);
        var completion = new ReadingCompletion(outcome.Json, retry.HasValue ? null : outcome.Error, retry);
        var saved = await jobs.FinishAsync(claim, completion, stopping);
        return new(claim.Id, claim.Attempt, saved ? outcome.Error ?? Wire.Published : Wire.LeaseLost);
    }
}
