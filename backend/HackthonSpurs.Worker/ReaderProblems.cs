using System.Net;
using System.Text.Json;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Worker;

internal static class ReaderProblems
{
    /// <summary>Keep only an allowlisted reader code for its declared status; never copy upstream text.</summary>
    /// <param name="response">Untrusted private-reader response received by the bounded HTTP client.</param>
    /// <param name="spur">Whether this is the page reader rather than the synthetic fixture.</param>
    /// <param name="ct">Cancellation for reading the bounded response body.</param>
    internal static async Task<ReadOutcome> ClassifyAsync(HttpResponseMessage response, bool spur, CancellationToken ct)
    {
        bool transient = response.StatusCode >= HttpStatusCode.InternalServerError ||
            response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout;
        var fallback = new ReadOutcome(Error: transient ? Errors.ReaderUnavailable.Code : Errors.ReaderRejected.Code,
            Retryable: transient);
        if (!spur || response.Content.Headers.ContentType?.MediaType != Media.Json ||
            response.Content.Headers.ContentLength > Policy.MaxReaderErrorBytes) return fallback;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        var bytes = new byte[Policy.MaxReaderErrorBytes + 1];
        int size = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, ct);
        if (size > Policy.MaxReaderErrorBytes) return fallback;
        try
        {
            using var json = JsonDocument.Parse(bytes.AsMemory(0, size), new JsonDocumentOptions { MaxDepth = Policy.ReadingDepth });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return fallback;
            string? code = null;
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (!property.NameEquals(Wire.Code)) continue;
                if (code != null || property.Value.ValueKind != JsonValueKind.String) return fallback;
                code = property.Value.GetString();
            }
            return code != null && Errors.ByCode.TryGetValue(code, out var entry) && entry.Reader &&
                entry.HttpStatus == (int)response.StatusCode ? new(Error: entry.Code, Retryable: transient) : fallback;
        }
        catch (JsonException) { return fallback; }
        catch (InvalidOperationException) { return fallback; }
    }
}
