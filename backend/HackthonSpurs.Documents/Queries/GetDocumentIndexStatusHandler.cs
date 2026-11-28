using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Queries;

public sealed record DocumentIndexStatusView(string State, int Attempt, int ChunkCount, string? FailureCode);

/// <summary>Read durable semantic-index status independently of the reading result.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class GetDocumentIndexStatusHandler(DocumentsDb db)
{
    /// <summary>Project both stages without loading the original, OCR body or vectors.</summary>
    /// <param name="id">Opaque document identifier requested by the caller.</param>
    /// <param name="ct">Cancellation for the database query.</param>
    public async Task<DocumentIndexStatusView?> HandleAsync(Guid id, CancellationToken ct)
    {
        var row = await (
            from d in db.Documents.AsNoTracking()
            where d.Id == id
            join i in db.IndexJobs.AsNoTracking() on d.Id equals i.DocumentId into indexes
            from i in indexes.DefaultIfEmpty()
            select new { d.SendToOcr, ReadingState = d.State, Index = i }
        ).SingleOrDefaultAsync(ct);

        if (row is null) return null;
        if (row.Index is { } index) return new(index.State.ToString(), index.Attempt, index.ChunkCount, index.FailureCode);
        if (!row.SendToOcr || row.ReadingState == JobState.Failed) return new(Wire.NotApplicable, 0, 0, null);
        return new(row.ReadingState == JobState.Completed ? Wire.NotIndexed : Wire.AwaitingReading, 0, 0, null);
    }
}
