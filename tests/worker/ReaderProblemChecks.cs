using System.Net;
using System.Text;
using System.Text.Json;
using HackthonSpurs.Resources;
using HackthonSpurs.Worker;

namespace HackthonSpurs.Worker.Tests;

internal static class ReaderProblemChecks
{
    /// <summary>Check the private reader error contract independently of the provider and database.</summary>
    internal static async Task RunAsync()
    {
        foreach (var entry in Errors.ByCode.Values.Where(e => e.Reader))
        {
            var body = JsonSerializer.Serialize(new { code = entry.Code, detail = "private diagnostic" });
            await CheckAsync(entry.HttpStatus!.Value, body, entry.Code, entry.HttpStatus >= 500);
        }
        await CheckAsync(422, """{"code":"reader_credentials_rejected"}""", "reader_credentials_rejected", false);
        await CheckAsync(422, """{"code":"reader_request_rejected"}""", "reader_request_rejected", false);
        await CheckAsync(422, """{"code":"invalid_reader_result"}""", "invalid_reader_result", false);
        foreach (string invalid in new[] { "{}", "[]", "null", "{", """{"code":false}""",
                     """{"code":"private diagnostic"}""", """{"code":"idempotency_conflict"}""",
                     """{"code":"reader_timeout"}""", """{"code":"reader_request_rejected","code":"reader_request_rejected"}""",
                     new string('x', Policy.MaxReaderErrorBytes + 1) })
            await CheckAsync(422, invalid, "reader_rejected", false);
        await CheckAsync(503, """{"code":"private diagnostic"}""", "reader_unavailable", true);
        await CheckAsync(504, """{"code":"reader_timeout"}""", "reader_timeout", true);
        await CheckAsync(422, """{"code":"reader_request_rejected"}""", "reader_rejected", false, spur: false);
        await CheckAsync(422, """{"code":"reader_request_rejected"}""", "reader_rejected", false, media: "text/plain");
        Console.WriteLine("PASS: known reader errors survive; wrong-status, untrusted, duplicate and oversized codes do not.");
    }

    /// <summary>Assert the sanitized outcome for one in-memory response.</summary>
    /// <param name="status">Simulated internal HTTP status.</param>
    /// <param name="json">Synthetic response body.</param>
    /// <param name="expected">Expected stable machine code.</param>
    /// <param name="retry">Expected retry classification.</param>
    /// <param name="spur">Whether to enable the private SPUR reader contract.</param>
    /// <param name="media">Simulated response media type.</param>
    private static async Task CheckAsync(int status, string json, string expected, bool retry,
        bool spur = true, string media = "application/json")
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(json, Encoding.UTF8, media)
        };
        var outcome = await ReaderProblems.ClassifyAsync(response, spur, CancellationToken.None);
        if (outcome.Json != null || outcome.Error != expected || outcome.Retryable != retry)
            throw new InvalidOperationException("Unexpected reader error classification.");
    }
}
