using HackthonSpurs.Resources;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(Wire.AspNetLogCategory, LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = Policy.MaxFixtureBytes);
var app = builder.Build();
using var embedded = typeof(Program).Assembly.GetManifestResourceStream(Wire.FixtureResource)!;
using var known = new MemoryStream();
await embedded.CopyToAsync(known);
var fixture = known.ToArray();

// Return the known CSV cell only when the private request matches the embedded bytes.
// request: Bounded internal upload and correlation headers. ct: Request cancellation.
app.MapPost(Wire.PrivateRead, async (HttpRequest request, CancellationToken ct) =>
{
    if (request.Headers[Wire.ReaderContractHeader] != Wire.Version ||
        !Guid.TryParseExact(request.Headers[Wire.DocumentHeader], "D", out var id))
        return Results.StatusCode(Errors.InvalidFixtureRequest.HttpStatus!.Value);
    if (request.ContentType != Media.Csv || request.ContentLength != fixture.Length)
        return Results.StatusCode(Errors.UnsupportedFixture.HttpStatus!.Value);
    var bytes = new byte[fixture.Length];
    await request.Body.ReadExactlyAsync(bytes, ct);
    if (!CryptographicOperations.FixedTimeEquals(bytes, fixture)) return Results.StatusCode(Errors.FixtureMismatch.HttpStatus!.Value);
    return Results.Ok(new
    {
        documentId = id,
        version = Wire.Version,
        readerVersion = Wire.FixtureVersion,
        fragments = new[] { new { method = Wire.Native, locator = new { kind = Wire.CsvCell, row = Wire.FixtureRow, column = Wire.FixtureColumn },
            text = Wire.FixtureText } },
        warnings = Array.Empty<object>()
    });
});
await app.RunAsync();
