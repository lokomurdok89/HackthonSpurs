using HackthonSpurs.Application.Commands;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Commands;

/// <summary>Use conditional writes so a stale read cannot reset an active index lease.</summary>
/// <param name="db">Scoped workspace database.</param>
public sealed class PostgresTranscriptReindex(DocumentsDb db) : ITranscriptReindex
{
    /// <inheritdoc />
    public async Task<ReindexOutcome> RequeueAsync(Guid id, CancellationToken ct)
    {
        var eligible = db.IndexJobs.Where(i => i.DocumentId == id && db.Documents.Any(d =>
            d.Id == id && d.SendToOcr && d.State == JobState.Completed));
        var reset = await eligible.Where(i => i.State == IndexState.Completed || i.State == IndexState.Failed)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.State, IndexState.Queued).SetProperty(i => i.Attempt, 0)
                .SetProperty(i => i.NextAttemptAt, (DateTimeOffset?)null).SetProperty(i => i.FailureCode, (string?)null)
                .SetProperty(i => i.ChunkCount, 0).SetProperty(i => i.Dimensions, (int?)null)
                .SetProperty(i => i.EmbeddingVersion, (string?)null), ct);
        if (reset > 0 || await eligible.AnyAsync(i => i.State == IndexState.Queued || i.State == IndexState.Processing, ct))
            return ReindexOutcome.Accepted;
        return await db.Documents.AnyAsync(d => d.Id == id, ct) ? ReindexOutcome.Ineligible : ReindexOutcome.MissingDocument;
    }
}
