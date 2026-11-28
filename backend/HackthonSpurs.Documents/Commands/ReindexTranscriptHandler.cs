using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Commands;

/// <summary>Requeue a terminal index without changing any reading job or accessing originals.</summary>
/// <param name="db">Scoped workspace database.</param>
public sealed class ReindexTranscriptHandler(DocumentsDb db)
{
    /// <summary>Reset only an existing eligible terminal index; pending requests coalesce.</summary>
    /// <param name="id">Document receipt selected by the administrator.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<bool?> HandleAsync(Guid id, CancellationToken ct)
    {
        var eligible = db.IndexJobs.Where(i => i.DocumentId == id && db.Documents.Any(d =>
            d.Id == id && d.SendToOcr && d.State == JobState.Completed));
        var reset = await eligible.Where(i => i.State == IndexState.Completed || i.State == IndexState.Failed)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.State, IndexState.Queued).SetProperty(i => i.Attempt, 0)
                .SetProperty(i => i.NextAttemptAt, (DateTimeOffset?)null).SetProperty(i => i.FailureCode, (string?)null)
                .SetProperty(i => i.ChunkCount, 0).SetProperty(i => i.Dimensions, (int?)null)
                .SetProperty(i => i.EmbeddingVersion, (string?)null), ct);
        if (reset > 0 || await eligible.AnyAsync(i => i.State == IndexState.Queued || i.State == IndexState.Processing, ct))
            return true;
        return await db.Documents.AnyAsync(d => d.Id == id, ct) ? false : null;
    }
}
