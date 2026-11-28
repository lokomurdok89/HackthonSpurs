using HackthonSpurs.Resources;

namespace HackthonSpurs.Api;

internal static class ApiProblems
{
    /// <summary>Build a public problem response without exposing an internal exception.</summary>
    /// <param name="error">Catalog entry for this response.</param>
    public static IResult Error(BackendError error) =>
        Results.Problem(statusCode: error.HttpStatus, title: error.Message, extensions: new Dictionary<string, object?> { [Wire.Code] = error.Code });

    /// <summary>Reject malformed upload input.</summary>
    public static IResult Invalid() => Error(Errors.InvalidRequest);
    /// <summary>Reject a route value that is not a document UUID.</summary>
    public static IResult InvalidId() => Error(Errors.InvalidDocumentId);
    /// <summary>Report that no document exists for the requested ID.</summary>
    public static IResult NotFound() => Error(Errors.DocumentNotFound);
    /// <summary>Reject reuse of an operation key for a different upload.</summary>
    public static IResult Conflict() => Error(Errors.IdempotencyConflict);
    /// <summary>Explain why a queued or processing job has no result yet.</summary>
    public static IResult Pending() => Error(Errors.ResultNotReady);
    /// <summary>Report that a terminally failed job has no reading.</summary>
    public static IResult Failed() => Error(Errors.ProcessingFailed);
    /// <summary>Reject a file exceeding the upload limit.</summary>
    public static IResult TooLarge() => Error(Errors.FileTooLarge);
    /// <summary>Reject a media type or file signature outside the active allowlist.</summary>
    public static IResult Unsupported() => Error(Errors.UnsupportedMediaType);
    /// <summary>Reject external image reading when the SPUR overlay is disabled.</summary>
    public static IResult OcrDisabled() => Error(Errors.OcrDisabled);
    /// <summary>Report a temporary service outage without leaking diagnostics.</summary>
    public static IResult Unavailable() => Error(Errors.ServiceUnavailable);
    /// <summary>Reject malformed or unbounded semantic search input.</summary>
    public static IResult InvalidSearch() => Error(Errors.InvalidSearch);
}
