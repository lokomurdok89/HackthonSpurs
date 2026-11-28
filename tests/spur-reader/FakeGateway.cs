using System.Net;
using System.Net.Http.Json;

namespace HackthonSpurs.SpurReader.Tests;

internal sealed class FakeGateway : HttpMessageHandler
{
    public int Calls { get; private set; }
    public Func<HttpRequestMessage, Task>? Inspect { get; set; }
    public Func<HttpResponseMessage> Reply { get; set; } = () => Message("DEMO");

    /// <summary>Inspect a test request and return the configured in-memory response.</summary>
    /// <param name="request">Incoming HTTP request.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        if (Inspect != null) await Inspect(request);
        return Reply();
    }

    /// <summary>Build a synthetic gateway completion for an adapter test.</summary>
    /// <param name="text">Synthetic assistant text.</param>
    /// <param name="reason">Completion finish reason.</param>
    public static HttpResponseMessage Message(string text, string reason = "stop") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { choices = new[] { new { finish_reason = reason, message = new { role = "assistant", content = text } } } })
    };
}
