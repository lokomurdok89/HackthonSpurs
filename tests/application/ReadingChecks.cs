using HackthonSpurs.Application.Processing;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Tests;

internal static class ReadingChecks
{
    /// <summary>Verify idle, successful, retryable and lease-lost reading attempts without hosting or a database.</summary>
    public static async Task RunAsync()
    {
        var jobs = new Jobs();
        var reader = new Reader();
        var useCase = new ProcessReadingJob(jobs, reader);
        var claim = new ReadingClaim(Guid.NewGuid(), Guid.NewGuid(), 1, 4, Media.Csv, "synthetic-hash", false);
        Check.Require(await useCase.RunOnceAsync(default) is null && reader.Calls == 0 && jobs.Writes == 0,
            "An idle poll must not call the reader or attempt a write.");
        jobs.Claim = claim;
        reader.Outcome = new(Json: "{}");
        var published = await useCase.RunOnceAsync(default);
        Check.Require(published == new JobAttemptReport(claim.Id, 1, Wire.Published) && jobs.FinishedClaim == claim &&
            reader.ReadClaim == claim && jobs.Completion == new ReadingCompletion("{}", null, null),
            "Reading success must publish under the original lease.");
        jobs.Save = false;
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Wire.LeaseLost,
            "A lease loss must not be reported as a publication.");

        jobs.Save = true;
        reader.Outcome = new(Error: Errors.ReaderUnavailable.Code, Retryable: true);
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Errors.ReaderUnavailable.Code &&
            jobs.Completion == new ReadingCompletion(null, null, TimeSpan.FromSeconds(Policy.FirstBackoffSeconds)),
            "A transient fixture failure should retry after the first backoff.");
        jobs.Claim = claim with { Attempt = Policy.MaxAttempts - 1 };
        await useCase.RunOnceAsync(default);
        Check.Require(jobs.Completion?.RetryDelay == TimeSpan.FromSeconds(Policy.SecondBackoffSeconds),
            "Subsequent permitted attempts should use the later backoff.");
        jobs.Claim = claim with { Attempt = Policy.MaxAttempts };
        await useCase.RunOnceAsync(default);
        Check.Require(jobs.Completion == new ReadingCompletion(null, Errors.ReaderUnavailable.Code, null),
            "The final fixture attempt must not retry.");

        jobs.Claim = claim with { SendToOcr = true };
        await useCase.RunOnceAsync(default);
        Check.Require(jobs.Completion == new ReadingCompletion(null, Errors.ReaderUnavailable.Code, null),
            "SPUR reading must not retry transient paid OCR calls.");
    }

    private sealed class Jobs : IReadingJobs
    {
        public ReadingClaim? Claim { get; set; }
        public bool Save { get; set; } = true;
        public int Writes { get; private set; }
        public ReadingClaim? FinishedClaim { get; private set; }
        public ReadingCompletion? Completion { get; private set; }

        /// <inheritdoc />
        public Task<ReadingClaim?> ClaimAsync(CancellationToken ct) => Task.FromResult(Claim);

        /// <inheritdoc />
        public Task<bool> FinishAsync(ReadingClaim claim, ReadingCompletion completion, CancellationToken ct)
        {
            Writes++;
            FinishedClaim = claim;
            Completion = completion;
            return Task.FromResult(Save);
        }
    }

    private sealed class Reader : IDocumentReader
    {
        public ReadingClaim? ReadClaim { get; private set; }
        public int Calls { get; private set; }
        public ReadOutcome Outcome { get; set; } = new();

        /// <inheritdoc />
        public Task<ReadOutcome> ReadAsync(ReadingClaim claim, CancellationToken stopping)
        {
            Calls++;
            ReadClaim = claim;
            return Task.FromResult(Outcome);
        }
    }
}
