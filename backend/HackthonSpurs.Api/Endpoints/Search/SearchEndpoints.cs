using System.Text.Json;
using HackthonSpurs.Api.Http;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Endpoints.Search;

internal static class SearchEndpoints
{
    private const int MaxRequestBytes = Policy.MaxSearchBody;
    private const int MaxQueryLength = Policy.MaxQueryLength;
    private sealed record SearchRequest(string? Query, int? TopK, Guid? DocumentId);

    /// <summary>Map the search and read-only search statistics operations.</summary>
    /// <param name="app">Workspace API endpoint builder.</param>
    public static void Map(WebApplication app)
    {
        var search = app.MapGroup(Wire.Search);
        search.MapPost("", Search);
        search.MapGet(Wire.Stats, Stats);
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
