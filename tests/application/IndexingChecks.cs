using System.Text.Json;
using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Application.Processing;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Tests;

internal static class IndexingChecks
{
    /// <summary>Verify index validation, vector order, retry policy and publication under the current lease.</summary>
    public static async Task RunAsync()
    {
        var jobs = new Jobs();
        var embeddings = new StubEmbeddings();
        var profile = new EmbeddingProfile(true, "synthetic", "v1", 2);
        var useCase = new ProcessIndexJob(jobs, embeddings, profile);
        Check.Require(await useCase.RunOnceAsync(default) is null && embeddings.Calls == 0,
            "An idle index poll must not call the provider.");

        var id = Guid.NewGuid();
        var claim = new IndexClaim(id, Guid.NewGuid(), 1, Reading(id));
        jobs.Claim = claim with { Reading = Reading(Guid.NewGuid()) };
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Errors.IndexSourceInvalid.Code &&
            jobs.Failure is { Retry: null } && embeddings.Calls == 0, "An invalid stored source must fail without embeddings.");

        jobs.Claim = claim;
        Check.Require(await useCase.RunOnceAsync(default) == new JobAttemptReport(id, 1, Wire.Published) &&
            jobs.Published is { } published && published.Claim == claim && published.Profile == profile &&
            published.Chunks.Select(c => c.Page).SequenceEqual([1, 3]) &&
            embeddings.Inputs!.SequenceEqual(["first", "third"]) && published.Vectors.Count == 2,
            "Indexing must submit ordered excerpts and publish their vectors only once under the same lease.");
        jobs.Save = false;
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Wire.LeaseLost, "A lost index lease is not a publication.");

        jobs.Save = true;
        embeddings.Failure = new EmbeddingFailure(Errors.EmbeddingProviderUnavailable, retryable: true);
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Errors.EmbeddingProviderUnavailable.Code &&
            jobs.Failure is { } failed && failed.Claim == claim && failed.Retry == TimeSpan.FromSeconds(Policy.FirstBackoffSeconds),
            "A transient provider failure should retry while the attempt budget remains.");
        jobs.Claim = claim with { Attempt = Policy.MaxAttempts - 1 };
        await useCase.RunOnceAsync(default);
        Check.Require(jobs.Failure is { } later && later.Retry == TimeSpan.FromSeconds(Policy.SecondBackoffSeconds),
            "A subsequent index failure should retry after the later backoff while attempts remain.");
        jobs.Claim = claim with { Attempt = Policy.MaxAttempts };
        await useCase.RunOnceAsync(default);
        Check.Require(jobs.Failure is { Retry: null }, "Exhausted index attempts must become terminal.");

        jobs.Claim = claim;
        embeddings.Failure = new OperationCanceledException();
        Check.Require((await useCase.RunOnceAsync(default))?.Outcome == Errors.EmbeddingProviderTimeout.Code,
            "A provider deadline without host shutdown must be classified as a timeout.");
        var writes = jobs.Writes;
        try
        {
            await useCase.RunOnceAsync(new CancellationToken(canceled: true));
            throw new InvalidOperationException("Host cancellation must propagate.");
        }
        catch (OperationCanceledException) { }
        Check.Require(jobs.Writes == writes, "Host shutdown must not write a synthetic provider failure.");
    }

    /// <summary>Create a minimal stored reading with two synthetic located excerpts.</summary>
    /// <param name="id">Receipt the text is bound to.</param>
    private static string Reading(Guid id) => JsonSerializer.Serialize(new
    {
        documentId = id, version = "1", readerVersion = "spur/1:synthetic",
        fragments = new[]
        {
            new { method = "ocr", locator = new { kind = "page", page = 1 }, text = "first" },
            new { method = "ocr", locator = new { kind = "page", page = 3 }, text = "third" }
        }
    });

    private sealed class Jobs : IIndexJobs
    {
        public IndexClaim? Claim { get; set; }
        public bool Save { get; set; } = true;
        public int Writes { get; private set; }
        public (IndexClaim Claim, string Code, TimeSpan? Retry)? Failure { get; private set; }
        public (IndexClaim Claim, IReadOnlyList<OcrIndexChunk> Chunks, IReadOnlyList<ReadOnlyMemory<float>> Vectors,
            EmbeddingProfile Profile)? Published { get; private set; }

        /// <inheritdoc />
        public Task<IndexClaim?> ClaimAsync(CancellationToken ct) => Task.FromResult(Claim);

        /// <inheritdoc />
        public Task<bool> FailAsync(IndexClaim claim, string code, TimeSpan? retryDelay, CancellationToken ct)
        {
            Writes++;
            Failure = (claim, code, retryDelay);
            return Task.FromResult(Save);
        }

        /// <inheritdoc />
        public Task<bool> PublishAsync(IndexClaim claim, IReadOnlyList<OcrIndexChunk> chunks,
            IReadOnlyList<ReadOnlyMemory<float>> vectors, EmbeddingProfile profile, CancellationToken ct)
        {
            Writes++;
            Published = (claim, chunks, vectors, profile);
            return Task.FromResult(Save);
        }
    }
}
