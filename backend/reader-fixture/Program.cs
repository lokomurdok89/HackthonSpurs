using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 1024);
var app = builder.Build();
using var embedded = typeof(Program).Assembly.GetManifestResourceStream("worker-demo.csv")!;
using var known = new MemoryStream();
embedded.CopyTo(known);
var fixture = known.ToArray();

app.MapPost("/internal/read", async (HttpRequest request, CancellationToken ct) =>
{
    if (request.Headers["X-Reader-Contract"] != "1" ||
        !Guid.TryParseExact(request.Headers["X-Document-Id"], "D", out var id))
        return Results.BadRequest();
    if (request.ContentType != "text/csv" || request.ContentLength != fixture.Length)
        return Results.StatusCode(415);
    var bytes = new byte[fixture.Length];
    await request.Body.ReadExactlyAsync(bytes, ct);
    if (!CryptographicOperations.FixedTimeEquals(bytes, fixture)) return Results.UnprocessableEntity();
    return Results.Ok(new {
        documentId = id, version = "1", readerVersion = "synthetic-fixture/1",
        fragments = new[] { new { method = "native", locator = new { kind = "csvCell", row = 2, column = 1 },
            text = "DEMO-ONLY-014" } }, warnings = Array.Empty<object>()
    });
});
await app.RunAsync();
