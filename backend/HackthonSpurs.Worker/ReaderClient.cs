using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace HackthonSpurs.Worker;

internal sealed record ReadOutcome(string? Json = null, string? Error = null, bool Retryable = false);
internal sealed class ReaderClient(HttpClient http, Uri endpoint, string root)
{
    public async Task<ReadOutcome> ReadAsync(Claim job, CancellationToken stopping)
    {
        // This adapter only handles the tiny known fixture. Verify the exact bytes sent,
        // not a name or a previously hashed stream that may change before the request.
        byte[] bytes;
        try
        {
            using var input = File.OpenRead(Path.Combine(root, $"{job.Id:N}.original"));
            if (job.ByteCount is <= 0 or > 1024 || input.Length != job.ByteCount)
                return new(Error: "original_mismatch");
            bytes = new byte[(int)job.ByteCount];
            await input.ReadExactlyAsync(bytes, stopping);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != JobQueue.FixtureHash)
                return new(Error: "original_mismatch");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(Error: "original_unavailable", Retryable: true); }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("X-Document-Id", job.Id.ToString("D"));
        request.Headers.Add("X-Reader-Contract", "1");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        try
        {
            using var response = await http.SendAsync(request, stopping);
            if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
                return new(Error: "reader_unavailable", Retryable: true);
            if (response.StatusCode != HttpStatusCode.OK) return new(Error: "reader_rejected");
            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                return new(Error: "invalid_reader_result");
            var body = await response.Content.ReadAsByteArrayAsync(stopping);
            return FixtureResult.Validate(body, job.Id);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        { return new(Error: "reader_timeout", Retryable: true); }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        { return new(Error: "invalid_reader_result"); }
        catch (HttpRequestException) { return new(Error: "reader_unavailable", Retryable: true); }
    }
}
