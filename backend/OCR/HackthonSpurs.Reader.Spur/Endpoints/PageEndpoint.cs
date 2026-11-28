using HackthonSpurs.Reader.Spur.Contracts;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Reader.Spur.Endpoints;

internal static class PageEndpoint
{
    public const int MaxBytes = Policy.MaxImageBytes;

    /// <summary>Validate one private page request and return OCR fragments or a located warning.</summary>
    /// <param name="request">Incoming HTTP request.</param>
    /// <param name="transcriber">Configured page transcription adapter.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Read(HttpRequest request, IPageTranscriber transcriber, CancellationToken ct)
    {
        if (request.Headers[Wire.ReaderContractHeader] != Wire.Version ||
            !Guid.TryParseExact(request.Headers[Wire.DocumentHeader], "D", out var doc) ||
            !int.TryParse(request.Headers[Wire.PageHeader], out var page) || page < 1)
            return Problem(Errors.InvalidReaderRequest);
        var mediaType = request.ContentType;
        if (mediaType is not (Media.Png or Media.Jpeg)) return Problem(Errors.UnsupportedImageType);
        if (request.ContentLength is not { } size) return Problem(Errors.LengthRequired);
        if (size > MaxBytes) return Problem(Errors.ImageTooLarge);
        if (size <= 0) return Problem(Errors.EmptyImage);
        var bytes = new byte[(int)size];
        try { await request.Body.ReadExactlyAsync(bytes, ct); }
        catch (IOException) { return Problem(Errors.IncompleteImage); }
        if (!MatchesSignature(bytes, mediaType)) return Problem(Errors.UnsupportedImageType);
        try
        {
            var text = await transcriber.TranscribeAsync(bytes, mediaType, ct);
            var locator = new { kind = Wire.Page, page };
            bool empty = string.IsNullOrWhiteSpace(text);
            return Results.Ok(new
            {
                documentId = doc,
                version = Wire.Version,
                readerVersion = transcriber.ReaderVersion,
                fragments = empty ? [] : new[] { new { method = Wire.Ocr, locator, text } },
                warnings = !empty ? [] : new[] { new { code = Errors.NoReadableText.Code, locator, message = Errors.NoReadableText.Message } }
            });
        }
        catch (ReaderFailure ex) { return Problem(ex.Error); }
    }

    /// <summary>Check the file prefix against the declared image format.</summary>
    /// <param name="bytes">Image bytes to inspect.</param>
    /// <param name="type">Declared image media type.</param>
    private static bool MatchesSignature(byte[] bytes, string type) => type == Media.Png
        ? bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[137, 80, 78, 71, 13, 10, 26, 10])
        : bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[255, 216, 255]);
    /// <summary>Return a safe reader error without forwarding provider diagnostics.</summary>
    /// <param name="error">Trusted reader error definition.</param>
    private static IResult Problem(BackendError error) => Results.Json(new { code = error.Code }, statusCode: error.HttpStatus);
}
