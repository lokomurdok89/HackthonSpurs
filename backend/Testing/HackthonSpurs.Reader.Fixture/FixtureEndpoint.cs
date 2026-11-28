using System.Security.Cryptography;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Reader.Fixture;

internal static class FixtureEndpoint
{
    /// <summary>Load the embedded synthetic fixture and map the private read endpoint.</summary>
    /// <param name="app">Private fixture API with its configured request limits.</param>
    internal static async Task MapAsync(WebApplication app)
    {
        using var embedded = typeof(FixtureEndpoint).Assembly.GetManifestResourceStream(Wire.FixtureResource)!;
        using var known = new MemoryStream();
        await embedded.CopyToAsync(known);
        var fixture = known.ToArray();
        app.MapPost(Wire.PrivateRead, (HttpRequest request, CancellationToken ct) => ReadAsync(request, fixture, ct));
    }

    /// <summary>Return the known CSV cell only when the request matches the embedded bytes.</summary>
    /// <param name="request">Bounded internal upload and its correlation headers.</param>
    /// <param name="fixture">Exact committed bytes of the synthetic CSV.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<IResult> ReadAsync(HttpRequest request, byte[] fixture, CancellationToken ct)
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
    }
}
