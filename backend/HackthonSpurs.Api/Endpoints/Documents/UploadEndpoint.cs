using HackthonSpurs.Api.Http;
using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Commands;
using HackthonSpurs.Documents.Storage;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace HackthonSpurs.Api.Endpoints.Documents;

internal static class UploadEndpoint
{
    /// <summary>Stage exactly one multipart file and dispatch the admission command.</summary>
    /// <param name="request">Incoming HTTP request.</param>
    /// <param name="store">Private staging store.</param>
    /// <param name="handler">Handler for this document operation.</param>
    /// <param name="config">Local reader enablement settings.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Handle(HttpRequest request, OriginalStore store, AcceptDocumentHandler handler, IConfiguration config, CancellationToken ct)
    {
        if (!Guid.TryParseExact(request.Headers[Wire.OperationKey], "D", out var key)) return ApiProblems.Invalid();
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type) ||
            !string.Equals(type.MediaType.Value, Media.Multipart, StringComparison.OrdinalIgnoreCase))
            return ApiProblems.Unsupported();

        var mode = request.Headers[Wire.ProviderHeader];
        bool spur = mode.Count == 1 && mode[0] == Wire.Spur;
        if (mode.Count != 0 && !spur) return ApiProblems.Invalid();
        if (spur && !config.GetValue<bool>(SettingNames.OcrEnabled)) return ApiProblems.OcrDisabled();

        var boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > Policy.MaxBoundary) return ApiProblems.Invalid();
        // Parse exactly one part. Avoid form buffering and never use the supplied name as a filesystem path.
        var reader = new MultipartReader(boundary, request.Body);
        StagedFile? file = null;
        try
        {
            var part = await ReadPartAsync(reader, ct);
            if (part?.GetContentDispositionHeader() is not { } disp || !disp.IsFileDisposition() ||
                HeaderUtilities.RemoveQuotes(disp.Name).Value != Wire.FilePart) return ApiProblems.Invalid();
            var declared = part.ContentType is null ? Media.Binary :
                MediaTypeHeaderValue.TryParse(part.ContentType, out var media) ? media.MediaType.Value!.ToLowerInvariant() : "";
            file = await store.StageAsync(part.Body, ct);
            if (file.Length == 0 || await ReadPartAsync(reader, ct) is not null) return ApiProblems.Invalid();
            if (spur && file.Length > OcrFormats.MaxDocumentBytes) return ApiProblems.TooLarge();
            var receipt = await handler.HandleAsync(new AcceptDocument(key, file, declared, spur), ct);
            return receipt.Outcome switch
            {
                Admission.Accepted => Results.Accepted($"{Wire.Documents}/{receipt.Id:D}", new { id = receipt.Id }),
                Admission.TooLarge => ApiProblems.TooLarge(),
                Admission.Conflict => ApiProblems.Conflict(),
                _ => ApiProblems.Unsupported()
            };
        }
        catch (UploadTooLargeException) { return ApiProblems.TooLarge(); }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge) { return ApiProblems.TooLarge(); }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status400BadRequest) { return ApiProblems.Invalid(); }
        catch (InvalidDataException) { return ApiProblems.Invalid(); }
        finally { file?.Dispose(); }
    }

    /// <summary>Read the next multipart section and classify incomplete framing as invalid input.</summary>
    /// <param name="reader">Reader for the current input.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    private static async Task<MultipartSection?> ReadPartAsync(MultipartReader reader, CancellationToken ct)
    {
        try { return await reader.ReadNextSectionAsync(ct); }
        catch (IOException ex) when (ex.GetType() == typeof(IOException))
        { throw new InvalidDataException(ResourceText.MultipartIncomplete, ex); }
    }
}
