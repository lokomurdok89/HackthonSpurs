using HackthonSpurs.Resources;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pgvector;

namespace HackthonSpurs.Documents.Embeddings;

/// <summary>Submit bounded text to SPUR and validate the returned vector positions and dimensions.</summary>
/// <param name="http">Server-owned HTTP client with timeout and response size limit.</param>
/// <param name="settings">Validated provider connection and vector profile.</param>
public sealed class SpurEmbeddingClient(HttpClient http, SpurEmbeddingSettings settings)
{
    private const int BatchSize = Policy.EmbedBatch;
    public const int MaxInputLength = Policy.EmbedInput;

    /// <summary>Embed one already bounded search input.</summary>
    /// <param name="text">Validated search text.</param>
    /// <param name="ct">Cancellation for the current request.</param>
    public async Task<Vector> EmbedAsync(string text, CancellationToken ct) =>
        (await EmbedBatchAsync([text], ct))[0];

    /// <summary>Embed the ordered input in bounded batches without retrying individual requests.</summary>
    /// <param name="texts">Text chunks or a single query, in expected output order.</param>
    /// <param name="ct">Cancellation for the full batch operation.</param>
    public async Task<IReadOnlyList<Vector>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (settings.Endpoint is null) throw new SpurEmbeddingException(Errors.EmbeddingsNotConfigured);
        if (texts.Count > Policy.MaxChunks || texts.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > MaxInputLength))
            throw new ArgumentException(ResourceText.EmbeddingBounds, nameof(texts));

        var vectors = new List<Vector>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
            vectors.AddRange(await SendAsync(batch, ct));
        return vectors;
    }

    /// <summary>Send and decode a single provider call without exposing upstream diagnostics.</summary>
    /// <param name="batch">Nonempty bounded group of embedding inputs.</param>
    /// <param name="ct">Cancellation for the provider operation.</param>
    private async Task<IReadOnlyList<Vector>> SendAsync(string[] batch, CancellationToken ct)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new EmbeddingRequest(settings.ModelId, batch));
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(Media.Json);
        request.Headers.Authorization = new AuthenticationHeaderValue(Wire.Bearer, settings.Key);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw StatusError(response.StatusCode);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(Policy.ProviderSeconds));
            await response.Content.LoadIntoBufferAsync(Policy.MaxEmbedReply, deadline.Token);
            var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: deadline.Token);
            return Validate(payload, batch.Length);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        {
            throw InvalidResponse();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new SpurEmbeddingException(Errors.EmbeddingProviderUnavailable, retryable: true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SpurEmbeddingException(Errors.EmbeddingProviderTimeout, retryable: true);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw InvalidResponse();
        }
    }

    /// <summary>Require one finite nonzero vector of the configured size for each unique index.</summary>
    /// <param name="payload">Untrusted provider response.</param>
    /// <param name="count">Number of inputs sent.</param>
    private Vector[] Validate(EmbeddingResponse? payload, int count)
    {
        if (payload?.Data is null || payload.Data.Length != count) throw InvalidResponse();
        var result = new Vector[count];
        foreach (var item in payload.Data)
        {
            if (item is not { Index: int i, Embedding: { } values } || i < 0 || i >= count || result[i] is not null ||
                values.Length != settings.VectorDimensions || values.Any(v => !float.IsFinite(v) || Math.Abs(v) > Policy.MaxVectorMagnitude) ||
                values.All(v => Math.Abs(v) < (float)Policy.MinVectorMagnitude))
                throw InvalidResponse();
            result[i] = new Vector(values);
        }
        return result;
    }

    /// <summary>Classify upstream status using safe public error identifiers.</summary>
    /// <param name="status">Provider HTTP response status.</param>
    private static SpurEmbeddingException StatusError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
            new(Errors.EmbeddingProviderTimeout, retryable: true),
        HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError =>
            new(Errors.EmbeddingProviderUnavailable, retryable: true),
        _ => new(Errors.EmbeddingProviderRejected)
    };

    /// <summary>Return a safe, terminal error for a malformed or incompatible response.</summary>
    private static SpurEmbeddingException InvalidResponse() => new(Errors.EmbeddingResponseInvalid);

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName(WireFields.Model)] string Model,
        [property: JsonPropertyName(WireFields.Input)] string[] Input);

    private sealed record EmbeddingResponse([property: JsonPropertyName(WireFields.Data)] EmbeddingItem?[]? Data);

    private sealed record EmbeddingItem(
        [property: JsonPropertyName(WireFields.Index)] int? Index,
        [property: JsonPropertyName(WireFields.Embedding)] float[]? Embedding);
}
