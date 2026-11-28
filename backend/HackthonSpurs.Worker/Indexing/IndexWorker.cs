using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Embeddings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HackthonSpurs.Worker.Indexing;

/// <summary>Run the optional indexing lane without access to original files or the OCR reader.</summary>
/// <param name="queue">Lease-aware PostgreSQL index queue.</param>
/// <param name="embeddings">Bounded SPUR embeddings client.</param>
/// <param name="profile">Validated embedding model, revision and dimensions.</param>
/// <param name="log">Safe log for worker outcomes.</param>
internal sealed class IndexWorker(IndexQueue queue, SpurEmbeddingClient embeddings, SpurEmbeddingSettings profile,
    ILogger<IndexWorker> log) : BackgroundService
{
    /// <summary>Process durable index jobs until shutdown; leave ambiguous writes to lease recovery.</summary>
    /// <param name="stopping">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (await queue.ClaimAsync(stopping) is { } claim)
                {
                    var outcome = await ProcessAsync(claim, stopping);
                    if (log.IsEnabled(LogLevel.Information))
                        log.LogInformation(ResourceText.IndexLog, claim.Id, claim.Attempt, outcome);
                    continue;
                }
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                log.LogWarning(ResourceText.IndexUnavailable);
            }
            await Task.Delay(TimeSpan.FromSeconds(Policy.PollSeconds), stopping);
        }
    }

    /// <summary>Read only the stored OCR fragments, embed their text, and publish under the current lease.</summary>
    /// <param name="claim">Lease and persisted reading, when scheduled by an explicit OCR upload.</param>
    /// <param name="stopping">Host shutdown token.</param>
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
            return await queue.PublishAsync(claim, chunks, vectors, profile, stopping) ? Wire.Published : Wire.LeaseLost;
        }
        catch (SpurEmbeddingException ex)
        {
            return await FailAsync(claim, ex.Code, ex.Retryable, stopping);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        {
            return await FailAsync(claim, Errors.EmbeddingProviderTimeout.Code, true, stopping);
        }
    }

    /// <summary>Apply a safe failure or retry disposition and report losing the lease separately.</summary>
    /// <param name="claim">Current lease for the document.</param>
    /// <param name="code">Stable failure identifier.</param>
    /// <param name="retry">Whether a subsequent attempt is permitted.</param>
    /// <param name="stopping">Host shutdown token.</param>
    private async Task<string> FailAsync(IndexClaim claim, string code, bool retry, CancellationToken stopping) =>
        await queue.FailAsync(claim, code, retry, stopping) ? code : Wire.LeaseLost;
}
