using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Queries;

/// <summary>Request progress for a single document.</summary>
/// <param name="Id">Opaque document identifier.</param>
public sealed record GetDocumentStatus(Guid Id);
/// <summary>Carry progress and failure details without loading the reading.</summary>
/// <param name="State">Persisted processing state.</param>
/// <param name="Attempt">Number of claims made.</param>
/// <param name="FailureCode">Safe terminal error code, if any.</param>
public sealed record DocumentStatusView(JobState State, int Attempt, string? FailureCode);

/// <summary>Read progress without loading originals or result JSON.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class GetDocumentStatusHandler(DocumentsDb db)
{
    /// <summary>Project progress only; return null for an unknown document.</summary>
    /// <param name="query">Document identifier to look up.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public Task<DocumentStatusView?> HandleAsync(GetDocumentStatus query, CancellationToken ct) =>
        db.Documents.AsNoTracking()
            .Where(d => d.Id == query.Id)
            .Select(d => new DocumentStatusView(d.State, d.Attempt, d.FailureCode))
            .SingleOrDefaultAsync(ct);
}
