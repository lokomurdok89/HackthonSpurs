using HackthonSpurs.Resources;
using System.Text.Json;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Queries;

namespace HackthonSpurs.Api;

internal static class SearchEndpoints
{
    private const int MaxRequestBytes = Policy.MaxSearchBody;
    private const int MaxQueryLength = Policy.MaxQueryLength;
    private sealed record SearchRequest(string? Query, int? TopK, Guid? DocumentId);

    /// <summary>Read the independent indexing state for an existing document receipt.</summary>
    /// <param name="id">Document UUID from the route.</param>
    /// <param name="handler">Read-only indexing projection.</param>
    /// <param name="ct">Cancellation for the API request.</param>
    public static async Task<IResult> Status(string id, GetDocumentIndexStatusHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var status = await handler.HandleAsync(key, ct);
        if (status is null) return ApiProblems.NotFound();
        return status.FailureCode is null
            ? Results.Ok(new { id = key, status.State, status.Attempt, status.ChunkCount })
            : Results.Ok(new { id = key, status.State, status.Attempt, status.ChunkCount, status.FailureCode });
    }

    /// <summary>Validate an explicit query and return ranked excerpts from current OCR indexes.</summary>
    /// <param name="request">Caller JSON body, bounded before deserialization.</param>
    /// <param name="search">Read-only document search service.</param>
    /// <param name="profile">Current provider model identification.</param>
    /// <param name="ct">Cancellation for the API request.</param>
    public static async Task<IResult> Search(HttpRequest request, DocumentSearchService search, SpurEmbeddingSettings profile, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return ApiProblems.Unsupported();
        if (request.ContentLength > MaxRequestBytes) return ApiProblems.TooLarge();
        var buffer = new byte[MaxRequestBytes + 1];
        int bytes = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
        if (bytes > MaxRequestBytes) return ApiProblems.TooLarge();
        SearchRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<SearchRequest>(buffer.AsSpan(0, bytes), JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return ApiProblems.InvalidSearch();
        }
        var query = body?.Query?.Trim();
        int topK = body?.TopK ?? Policy.DefaultTopK;
        if (query is not { Length: > 0 and <= MaxQueryLength } || query.Contains('\0') || topK is < 1 or > Policy.MaxTopK)
            return ApiProblems.InvalidSearch();

        try
        {
            var results = await search.SearchAsync(query, topK, body?.DocumentId, ct);
            return Results.Ok(new { query, model = profile.ModelId, results });
        }
        catch (SpurEmbeddingException ex)
        {
            return ApiProblems.Error(ex.Error);
        }
    }

    /// <summary>Count compatible index records without sending a provider request.</summary>
    /// <param name="search">Read-only document search service.</param>
    /// <param name="ct">Cancellation for the API request.</param>
    public static async Task<IResult> Stats(DocumentSearchService search, CancellationToken ct) =>
        Results.Ok(await search.GetStatsAsync(ct));
}
