using HackthonSpurs.Documents.Commands;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Api;

internal static class IndexInspectionEndpoints
{
    /// <summary>Inspect published chunks without scheduling work.</summary>
    /// <param name="id">Requested receipt UUID.</param>
    /// <param name="search">Read-only chunk projection.</param>
    /// <param name="ct">Request cancellation.</param>
    public static async Task<IResult> Chunks(string id, DocumentSearchService search, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var detail = await search.GetDocumentDetailAsync(key, ct);
        return detail is null ? ApiProblems.NotFound() : Results.Ok(detail);
    }

    /// <summary>Schedule embeddings from stored OCR only; never submit a file to a reader.</summary>
    /// <param name="id">Requested receipt UUID.</param>
    /// <param name="handler">Durable index command.</param>
    /// <param name="profile">Active embeddings configuration.</param>
    /// <param name="ct">Request cancellation.</param>
    public static async Task<IResult> Reindex(string id, ReindexTranscriptHandler handler, SpurEmbeddingSettings profile, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        if (!profile.IsConfigured) return ApiProblems.Error(Errors.EmbeddingsNotConfigured);
        return await handler.HandleAsync(key, ct) switch
        {
            true => Results.Accepted(), false => ApiProblems.Error(Errors.IndexNotReady), null => ApiProblems.NotFound()
        };
    }
}
