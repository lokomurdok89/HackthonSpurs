using HackthonSpurs.Reader.Spur.Contracts;
using HackthonSpurs.Resources;
using System.Net;
using System.Text.Json;

namespace HackthonSpurs.Reader.Spur.Provider;

// Only this adapter knows the provider URL, authentication, message shape and response envelope.
/// <summary>Isolate external vision requests and validate the provider envelope.</summary>
/// <param name="http">Configured transport.</param>
/// <param name="settings">Validated process settings.</param>
internal sealed class SpurVisionClient(HttpClient http, SpurSettings settings) : IPageTranscriber
{
    public string ReaderVersion => Wire.SpurPrefix + settings.Model;

    /// <summary>Create the bounded gateway transport with redirects, proxy and cookies disabled.</summary>
    public static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false
    })
    { Timeout = TimeSpan.FromSeconds(Policy.ProviderSeconds), MaxResponseContentBufferSize = Policy.MaxVisionReply };

    /// <summary>Return the visible text for one image or raise a classified reader failure.</summary>
    /// <param name="image">Bytes of one validated page image.</param>
    /// <param name="mediaType">PNG or JPEG media type.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<string> TranscribeAsync(byte[] image, string mediaType, CancellationToken ct)
    {
        if (image.Length > settings.ImageTarget) throw new ReaderFailure(Errors.OcrPageOverBudget);
        var bytes = SpurPayload.Encode(settings.Model, image, mediaType);
        if (bytes.Length > settings.JsonLimit) throw new ReaderFailure(Errors.OcrJsonOverBudget);
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = settings.Authorization;
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Media.Json);
        try
        {
            using var reply = await http.SendAsync(request, ct);
            if (reply.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ReaderFailure(Errors.ReaderCredentialsRejected);
            if (reply.StatusCode >= HttpStatusCode.InternalServerError || reply.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout)
                throw new ReaderFailure(Errors.ReaderUnavailable);
            if (reply.StatusCode != HttpStatusCode.OK) throw new ReaderFailure(Errors.ReaderRequestRejected);
            if (reply.Content.Headers.ContentType?.MediaType != Media.Json) throw InvalidResult();
            using var json = JsonDocument.Parse(await reply.Content.ReadAsByteArrayAsync(ct), new JsonDocumentOptions { MaxDepth = Policy.VisionDepth });
            return ReadText(json.RootElement);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ReaderFailure(Errors.ReaderTimeout); }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded) { throw InvalidResult(); }
        catch (HttpRequestException) { throw new ReaderFailure(Errors.ReaderUnavailable); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw InvalidResult(); }
    }

    /// <summary>Classify a malformed or incomplete provider completion.</summary>
    private static ReaderFailure InvalidResult() => new(Errors.InvalidReaderResult);

    /// <summary>Extract a single finished assistant transcription and reject unsupported responses.</summary>
    /// <param name="root">Provider completion envelope.</param>
    private static string ReadText(JsonElement root)
    {
        if (!root.TryGetProperty(WireFields.Choices, out var choices) || choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() != 1) throw InvalidResult();
        var choice = choices[0];
        if (!choice.TryGetProperty(WireFields.FinishReason, out var reason) || reason.GetString() != WireFields.Stop ||
            !choice.TryGetProperty(WireFields.Message, out var message) ||
            !message.TryGetProperty(WireFields.Role, out var role) || role.GetString() != WireFields.Assistant ||
            (message.TryGetProperty(WireFields.Refusal, out var refusal) && refusal.ValueKind != JsonValueKind.Null) ||
            (message.TryGetProperty(WireFields.ToolCalls, out var tools) && tools.ValueKind != JsonValueKind.Null &&
             !(tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() == 0)) ||
            !message.TryGetProperty(WireFields.Content, out var value) || value.ValueKind != JsonValueKind.String)
            throw InvalidResult();
        var text = value.GetString()!;
        if (text.Length > Policy.MaxTranscriptLength || text.Contains('\0')) throw InvalidResult();
        return text;
    }
}
