using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Queries;

/// <summary>Request the stored reading and its publication state.</summary>
/// <param name="Id">Opaque document identifier.</param>
public sealed record GetDocumentResult(Guid Id);
/// <summary>Carry only the fields needed to serve the result endpoint.</summary>
/// <param name="State">Persisted processing state.</param>
/// <param name="ResultJson">Published reading, if available.</param>
public sealed record DocumentResultView(JobState State, string? ResultJson);

/// <summary>Read publication state and stored evidence without modifying the job.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class GetDocumentResultHandler(DocumentsDb db)
{
    /// <summary>Read the stored result and state without claiming work; return null for an unknown document.</summary>
    /// <param name="query">Document identifier to look up.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public Task<DocumentResultView?> HandleAsync(GetDocumentResult query, CancellationToken ct) =>
        db.Documents.AsNoTracking()
            .Where(d => d.Id == query.Id)
            .Select(d => new DocumentResultView(d.State, d.ResultJson))
            .SingleOrDefaultAsync(ct);
}
