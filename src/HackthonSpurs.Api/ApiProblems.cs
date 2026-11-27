namespace HackthonSpurs.Api;

internal static class ApiProblems
{
    private static IResult Error(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult Invalid() => Error(400, "invalid_request", "Exactly one non-empty file and a valid Idempotency-Key are required.");
    public static IResult InvalidId() => Error(400, "invalid_document_id", "Invalid document ID.");
    public static IResult NotFound() => Error(404, "document_not_found", "Document not found.");
    public static IResult Conflict() => Error(409, "idempotency_conflict", "This upload key was used for different file content.");
    public static IResult Pending() => Error(409, "result_not_ready", "Document is queued or processing.");
    public static IResult Failed() => Error(409, "processing_failed", "Processing failed; no result is available.");
    public static IResult TooLarge() => Error(413, "file_too_large", "File exceeds the upload size limit.");
    public static IResult Unsupported() => Error(415, "unsupported_media_type", "Unsupported file or request format.");
    public static IResult Unavailable() => Error(503, "service_unavailable", "Service temporarily unavailable.");
}
