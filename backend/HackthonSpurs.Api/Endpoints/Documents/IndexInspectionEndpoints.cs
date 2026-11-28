using HackthonSpurs.Api.Http;
using HackthonSpurs.Application.Commands;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Endpoints.Documents;

internal static class IndexInspectionEndpoints
{
    /// <summary>Read the independent indexing state for an existing document receipt.</summary>
    /// <param name="id">Document UUID from the route.</param>
    /// <param name="reads">Read-only indexing projection.</param>
    /// <param name="ct">Cancellation for the API request.</param>
    public static async Task<IResult> Status(string id, IDocumentReads reads, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var status = await reads.GetIndexStatusAsync(key, ct);
        if (status is null) return ApiProblems.NotFound();
        return status.FailureCode is null
            ? Results.Ok(new { id = key, status.State, status.Attempt, status.ChunkCount })
            : Results.Ok(new { id = key, status.State, status.Attempt, status.ChunkCount, status.FailureCode });
    }

    /// <summary>Inspect published chunks without scheduling work.</summary>
    /// <param name="id">Requested receipt UUID.</param>
    /// <param name="reads">Read-only chunk projection.</param>
    /// <param name="ct">Request cancellation.</param>
    public static async Task<IResult> Chunks(string id, IIndexedDocumentReads reads, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var detail = await reads.GetDocumentDetailAsync(key, ct);
        return detail is null ? ApiProblems.NotFound() : Results.Ok(detail);
    }

    /// <summary>Schedule embeddings from stored OCR only; never submit a file to a reader.</summary>
    /// <param name="id">Requested receipt UUID.</param>
    /// <param name="handler">Durable index command.</param>
    /// <param name="ct">Request cancellation.</param>
    public static async Task<IResult> Reindex(string id, ReindexTranscriptHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        return await handler.HandleAsync(key, ct) switch
        {
            ReindexOutcome.Accepted => Results.Accepted(),
            ReindexOutcome.Disabled => ApiProblems.Error(Errors.EmbeddingsNotConfigured),
            ReindexOutcome.Ineligible => ApiProblems.Error(Errors.IndexNotReady),
            _ => ApiProblems.NotFound()
        };
    }
}
