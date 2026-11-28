using HackthonSpurs.Application.Embeddings;

namespace HackthonSpurs.Application.Commands;

public enum ReindexOutcome { Accepted, MissingDocument, Ineligible, Disabled }

public interface ITranscriptReindex
{
    /// <summary>Atomically reset an eligible terminal index, or coalesce a request with work already in progress.</summary>
    /// <param name="id">Document receipt selected by the administrator.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<ReindexOutcome> RequeueAsync(Guid id, CancellationToken ct);
}

/// <summary>Request indexing of a stored transcript without resubmitting an original for OCR.</summary>
/// <param name="index">Durable index writer.</param>
/// <param name="profile">Current embedding availability and profile.</param>
public sealed class ReindexTranscriptHandler(ITranscriptReindex index, EmbeddingProfile profile)
{
    /// <summary>Reject reindexing when embeddings are disabled; otherwise apply the atomic index command.</summary>
    /// <param name="id">Document receipt selected by the administrator.</param>
    /// <param name="ct">Request cancellation.</param>
    public Task<ReindexOutcome> HandleAsync(Guid id, CancellationToken ct) =>
        profile.IsConfigured ? index.RequeueAsync(id, ct) : Task.FromResult(ReindexOutcome.Disabled);
}
