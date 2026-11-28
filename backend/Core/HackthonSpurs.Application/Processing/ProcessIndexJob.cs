using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Processing;

/// <summary>Index one persisted OCR result without polling or reading original files.</summary>
/// <param name="jobs">Lease-aware store for index attempts.</param>
/// <param name="embeddings">Boundary for ordered text embeddings.</param>
/// <param name="profile">Expected model, vector revision and dimensions.</param>
public sealed class ProcessIndexJob(IIndexJobs jobs, ITextEmbeddings embeddings, EmbeddingProfile profile)
{
    /// <summary>Claim one job and coordinate its attempt; return null when no work was claimed.</summary>
    /// <param name="stopping">Worker shutdown token.</param>
    public async Task<JobAttemptReport?> RunOnceAsync(CancellationToken stopping)
    {
        var claim = await jobs.ClaimAsync(stopping);
        if (claim is null) return null;
        return new(claim.Id, claim.Attempt, await ProcessAsync(claim, stopping));
    }

    /// <summary>Extract bounded excerpts, embed their text and publish only under the current lease.</summary>
    /// <param name="claim">Owned lease and persisted OCR result.</param>
    /// <param name="stopping">Worker shutdown token.</param>
    private async Task<string> ProcessAsync(IndexClaim claim, CancellationToken stopping)
    {
        var chunks = OcrIndexChunks.Create(claim.Reading, claim.Id);
        if (chunks is not { Count: > 0 })
            return await FailAsync(claim, Errors.IndexSourceInvalid.Code, false, stopping);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        deadline.CancelAfter(TimeSpan.FromMinutes(Policy.IndexMinutes));
        try
        {
            var vectors = await embeddings.EmbedBatchAsync(chunks.Select(c => c.Text).ToArray(), deadline.Token);
            return await jobs.PublishAsync(claim, chunks, vectors, profile, stopping) ? Wire.Published : Wire.LeaseLost;
        }
        catch (EmbeddingFailure ex)
        {
            return await FailAsync(claim, ex.Code, ex.Retryable, stopping);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        {
            return await FailAsync(claim, Errors.EmbeddingProviderTimeout.Code, true, stopping);
        }
    }

    /// <summary>Apply retry policy and a safe failure code, unless another attempt owns the lease.</summary>
    /// <param name="claim">Current lease for the document.</param>
    /// <param name="code">Stable failure identifier.</param>
    /// <param name="retryable">Whether a subsequent attempt may succeed.</param>
    /// <param name="stopping">Worker shutdown token.</param>
    private async Task<string> FailAsync(IndexClaim claim, string code, bool retryable, CancellationToken stopping) =>
        await jobs.FailAsync(claim, code, RetrySchedule.AfterFailure(true, retryable, claim.Attempt), stopping) ? code : Wire.LeaseLost;
}
