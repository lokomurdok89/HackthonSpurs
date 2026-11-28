#nullable enable

using System.Net;
using System.Text;
using System.Text.Json;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Worker;

namespace HackthonSpurs.Worker.Tests;

internal static class EmbeddingReview
{
    /// <summary>Exercise provider decoding, failure classification and page slicing without network access.</summary>
    internal static async Task RunAsync()
    {
        using var handler = new Stub();
        using var http = new HttpClient(handler);
        var client = new SpurEmbeddingClient(http, new(new Uri("https://stub.invalid"), "offline", "test", "1", 2));
        var vectors = await client.EmbedBatchAsync(Enumerable.Range(1, 9).Select(n => n.ToString()).ToArray(), default);
        if (handler.Calls != 2 || !vectors.Select(v => v.ToArray()[0]).SequenceEqual(Enumerable.Range(1, 9).Select(i => (float)i)))
            throw new InvalidOperationException("Batch boundaries or provider indexes were ignored.");

        foreach (var rows in new[]
        {
            "[]", "[{\"embedding\":[1,1]}]", "[{\"index\":0,\"embedding\":[0,0]}]",
            "[{\"index\":0,\"embedding\":[1]}]", "[{\"index\":0,\"embedding\":[1e80,1]}]",
            "[{\"index\":1,\"embedding\":[1,1]}]"
        })
        {
            handler.Body = "{\"data\":" + rows + "}";
            await ExpectFailure(() => client.EmbedAsync("1", default), "embedding_response_invalid", false);
        }
        handler.Body = "{\"data\":[{\"index\":0,\"embedding\":[1,1]},{\"index\":0,\"embedding\":[1,1]}]}";
        await ExpectFailure(() => client.EmbedBatchAsync(["1", "2"], default), "embedding_response_invalid", false);
        handler.Body = "sensitive-upstream-detail";
        handler.Status = HttpStatusCode.TooManyRequests;
        await ExpectFailure(() => client.EmbedAsync("1", default), "embedding_provider_unavailable", true);
        handler.Status = HttpStatusCode.Unauthorized;
        await ExpectFailure(() => client.EmbedAsync("1", default), "embedding_provider_rejected", false);
        var disabled = new SpurEmbeddingClient(http, new(null, "", "", "", 2));
        int before = handler.Calls;
        await ExpectFailure(() => disabled.EmbedAsync("1", default), "embeddings_not_configured", false);
        if (before != handler.Calls) throw new InvalidOperationException("Disabled integration called provider.");

        var id = Guid.NewGuid();
        string first = new string('x', SpurEmbeddingClient.MaxInputLength - 1) + "📄end";
        var json = JsonSerializer.Serialize(new
        {
            documentId = id, version = "1", readerVersion = "spur/1:offline",
            fragments = new[] { Fragment(1, first), Fragment(3, "third") }
        });
        var chunks = OcrIndexChunks.Create(json, id) ?? throw new InvalidOperationException("Rejected valid reading.");
        if (!chunks.Select(c => c.Page).SequenceEqual([1, 1, 3]) || chunks[0].Text + chunks[1].Text != first ||
            !chunks[1].Text.StartsWith("📄", StringComparison.Ordinal) || chunks[2].Text != "third" ||
            OcrIndexChunks.Create(json, Guid.NewGuid()) != null)
            throw new InvalidOperationException("Incorrect document, locator or Unicode boundary.");
        Console.WriteLine("PASS: offline embedding batches, error isolation, profile dimensions and page-level Unicode boundaries.");
    }

    /// <summary>Construct one located synthetic OCR fragment.</summary>
    /// <param name="page">One-based source page number.</param>
    /// <param name="text">Synthetic transcription.</param>
    private static object Fragment(int page, string text) => new { method = "ocr", text, locator = new { kind = "page", page } };

    /// <summary>Require a stable safe code and the expected retry policy.</summary>
    /// <param name="operation">Embedding operation under test.</param>
    /// <param name="code">Expected public identifier.</param>
    /// <param name="retry">Expected retry classification.</param>
    private static async Task ExpectFailure(Func<Task> operation, string code, bool retry)
    {
        try { await operation(); }
        catch (SpurEmbeddingException ex) when (ex.Code == code && ex.Message == code && ex.Retryable == retry) { return; }
        throw new InvalidOperationException("Missing expected safe provider failure: " + code);
    }

    private sealed class Stub : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        /// <summary>Return synthetic vectors in reverse provider order or an injected failure.</summary>
        /// <param name="request">In-memory embedding request.</param>
        /// <param name="ct">Cancellation token for reading the synthetic payload.</param>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var input = doc.RootElement.GetProperty("input");
            var rows = input.EnumerateArray().Select((value, index) =>
                new { index, embedding = new[] { int.Parse(value.GetString()!), 1 } }).Reverse();
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body ?? JsonSerializer.Serialize(new { data = rows }), Encoding.UTF8, "application/json")
            };
        }
    }
}
