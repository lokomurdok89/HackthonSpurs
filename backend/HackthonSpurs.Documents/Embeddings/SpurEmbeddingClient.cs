using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Pgvector;

namespace HackthonSpurs.Documents.Embeddings;

public sealed record SpurEmbeddingSettings(string BaseUrl, string ApiKey, string EmbedModel)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(EmbedModel);
}

public sealed class SpurEmbeddingException(string code, string message, int statusCode = 502) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class SpurEmbeddingClient(HttpClient http, SpurEmbeddingSettings settings)
{
    private const int BatchSize = 32;

    public bool IsConfigured => settings.IsConfigured;
    public string Model => settings.EmbedModel;

    public async Task<Vector> EmbedAsync(string text, CancellationToken ct)
    {
        var results = await EmbedBatchAsync([text], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<Vector>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (!settings.IsConfigured)
            throw new SpurEmbeddingException(
                "embeddings_not_configured",
                "SPUR embeddings are not configured. Set SPUR_API_KEY (and optionally SPUR_BASE_URL / SPUR_EMBED_MODEL) in .env.",
                503);

        if (texts.Count == 0)
            return [];

        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/embeddings";
        var vectors = new List<Vector>(texts.Count);

        for (var offset = 0; offset < texts.Count; offset += BatchSize)
        {
            var count = Math.Min(BatchSize, texts.Count - offset);
            var batch = new string[count];
            for (var i = 0; i < count; i++)
                batch[i] = texts[offset + i];

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new EmbeddingRequest(settings.EmbedModel, batch))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new SpurEmbeddingException("embedding_provider_unreachable", $"Could not reach SPUR embeddings endpoint: {ex.Message}", 503);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new SpurEmbeddingException("embedding_provider_timeout", "SPUR embeddings request timed out.", 504);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync(ct);
                    if (detail.Length > 300) detail = detail[..300];
                    throw new SpurEmbeddingException(
                        "embedding_provider_error",
                        $"SPUR embeddings API returned {(int)response.StatusCode}: {detail}",
                        502);
                }

                var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
                    ?? throw new SpurEmbeddingException("embedding_provider_error", "Empty response from SPUR embeddings API.");

                if (payload.Data is null || payload.Data.Count != batch.Length)
                    throw new SpurEmbeddingException("embedding_provider_error", "Unexpected embedding count returned by SPUR API.");

                foreach (var item in payload.Data.OrderBy(d => d.Index))
                {
                    if (item.Embedding is null || item.Embedding.Length == 0)
                        throw new SpurEmbeddingException("embedding_provider_error", "SPUR API returned an empty embedding vector.");
                    vectors.Add(new Vector(item.Embedding));
                }
            }
        }

        return vectors;
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input);

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingItem>? Data { get; set; }
    }

    private sealed class EmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
