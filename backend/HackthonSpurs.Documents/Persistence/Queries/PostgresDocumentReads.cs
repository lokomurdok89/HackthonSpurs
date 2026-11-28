using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Queries;

/// <summary>Read only the receipt columns each document operation needs.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class PostgresDocumentReads(DocumentsDb db) : IDocumentReads
{
    /// <inheritdoc />
    public async Task<DocumentStatusView?> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Documents.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.State, d.Attempt, d.FailureCode }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.State.ToString(), row.Attempt, row.FailureCode, row.State == JobState.Failed);
    }

    /// <inheritdoc />
    public async Task<DocumentResultView?> GetResultAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Documents.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.State, d.ResultJson }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.State switch
        {
            JobState.Completed => ReadingAvailability.Published,
            JobState.Failed => ReadingAvailability.Failed,
            _ => ReadingAvailability.Pending
        }, row.ResultJson);
    }

    /// <inheritdoc />
    public async Task<DocumentIndexStatusView?> GetIndexStatusAsync(Guid id, CancellationToken ct)
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
