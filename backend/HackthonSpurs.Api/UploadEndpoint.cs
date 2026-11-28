using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Storage;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace HackthonSpurs.Api;

internal static class UploadEndpoint
{
    public static async Task<IResult> Handle(HttpRequest request, OriginalStore store, Ingestion ingestion, CancellationToken ct)
    {
        if (!Guid.TryParseExact(request.Headers["Idempotency-Key"], "D", out var key)) return ApiProblems.Invalid();
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type) ||
            !string.Equals(type.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return ApiProblems.Unsupported();

        var boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 70) return ApiProblems.Invalid();
        // Parse exactly one part. Avoid form buffering and never use the supplied name as a filesystem path.
        var reader = new MultipartReader(boundary, request.Body);
        StagedFile? file = null;
        try
        {
            var part = await ReadPartAsync(reader, ct);
            if (part?.GetContentDispositionHeader() is not { } disp || !disp.IsFileDisposition() ||
                HeaderUtilities.RemoveQuotes(disp.Name).Value != "file") return ApiProblems.Invalid();
            var declared = part.ContentType is null ? "application/octet-stream" :
                MediaTypeHeaderValue.TryParse(part.ContentType, out var media) ? media.MediaType.Value!.ToLowerInvariant() : "";
            file = await store.StageAsync(part.Body, ct);
            if (file.Length == 0 || await ReadPartAsync(reader, ct) is not null) return ApiProblems.Invalid();
            var receipt = await ingestion.AcceptAsync(key, file, declared, ct);
            return receipt.Outcome switch
            {
                Admission.Accepted => Results.Accepted($"/api/documents/{receipt.Id:D}", new { id = receipt.Id }),
                Admission.Conflict => ApiProblems.Conflict(),
                _ => ApiProblems.Unsupported()
            };
        }
        catch (UploadTooLargeException) { return ApiProblems.TooLarge(); }
        catch (BadHttpRequestException e) when (e.StatusCode == 413) { return ApiProblems.TooLarge(); }
        catch (BadHttpRequestException e) when (e.StatusCode == 400) { return ApiProblems.Invalid(); }
        catch (InvalidDataException) { return ApiProblems.Invalid(); }
        finally { file?.Dispose(); }
    }

    private static async Task<MultipartSection?> ReadPartAsync(MultipartReader reader, CancellationToken ct)
    {
        try { return await reader.ReadNextSectionAsync(ct); }
        catch (IOException ex) when (ex.GetType() == typeof(IOException))
        { throw new InvalidDataException("Incomplete multipart framing.", ex); }
    }
}
