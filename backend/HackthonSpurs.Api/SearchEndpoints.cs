using System.Text.Json;
using HackthonSpurs.Documents.Embeddings;

namespace HackthonSpurs.Api;

internal static class SearchEndpoints
{
    public sealed record IndexRequest(string? SourceName, List<ChunkInput>? Chunks);
    public sealed record ChunkInput(string? Locator, string Text);
    public sealed record SearchRequest(string Query, int? TopK, Guid? DocumentId);

    public static async Task<IResult> Index(
        string id,
        HttpRequest httpRequest,
        DocumentSearchService search,
        CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var documentId))
            return ApiProblems.InvalidId();

        IndexRequest? body = null;
        if (httpRequest.ContentLength is > 0)
        {
            try
            {
                body = await httpRequest.ReadFromJsonAsync<IndexRequest>(ct);
            }
            catch (JsonException)
            {
                return Problem(400, "invalid_request", "Invalid JSON payload.");
            }
        }

        IReadOnlyList<ExtractedChunk>? explicitChunks = body?.Chunks is { Count: > 0 }
            ? body.Chunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .Select(c => new ExtractedChunk(c.Locator ?? "document", c.Text))
                .ToList()
            : null;

        try
        {
            var result = await search.IndexDocumentAsync(documentId, body?.SourceName, explicitChunks, ct);
            return result is null ? ApiProblems.NotFound() : Results.Ok(result);
        }
        catch (SpurEmbeddingException ex)
        {
            return Problem(ex.StatusCode, ex.Code, ex.Message);
        }
    }

    public static async Task<IResult> Search(
        HttpRequest httpRequest,
        DocumentSearchService search,
        SpurEmbeddingClient embeddings,
        CancellationToken ct)
    {
        SearchRequest? body;
        try
        {
            body = await httpRequest.ReadFromJsonAsync<SearchRequest>(ct);
        }
        catch (JsonException)
        {
            return Problem(400, "invalid_request", "Invalid JSON payload.");
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Query))
            return Problem(400, "invalid_query", "A non-empty search query is required.");

        try
        {
            var hits = await search.SearchAsync(body.Query, body.TopK ?? 8, body.DocumentId, ct);
            return Results.Ok(new
            {
                query = body.Query.Trim(),
                model = embeddings.Model,
                results = hits
            });
        }
        catch (SpurEmbeddingException ex)
        {
            return Problem(ex.StatusCode, ex.Code, ex.Message);
        }
    }

    public static async Task<IResult> Stats(DocumentSearchService search, CancellationToken ct) =>
        Results.Ok(await search.GetStatsAsync(ct));

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
