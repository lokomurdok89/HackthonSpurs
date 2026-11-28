using HackthonSpurs.Api.Http;
using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Queries;

namespace HackthonSpurs.Api.Endpoints.Documents;

internal static class DocumentReadEndpoints
{
    /// <summary>Map the status projection to HTTP, omitting failure details for nonfailed jobs.</summary>
    /// <param name="id">Requested document UUID.</param>
    /// <param name="handler">Handler for this document operation.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Status(string id, GetDocumentStatusHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await handler.HandleAsync(new GetDocumentStatus(key), ct);
        if (row is null) return ApiProblems.NotFound();
        var state = row.State.ToString();
        return row.State == JobState.Failed
            ? Results.Ok(new { id = key, state, row.Attempt, row.FailureCode })
            : Results.Ok(new { id = key, state, row.Attempt });
    }

    /// <summary>Map the persisted reading or job state to the public result response.</summary>
    /// <param name="id">Requested document UUID.</param>
    /// <param name="handler">Handler for this document operation.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Result(string id, GetDocumentResultHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await handler.HandleAsync(new GetDocumentResult(key), ct);
        return row?.State switch
        {
            null => ApiProblems.NotFound(),
            JobState.Completed => Results.Text(row.ResultJson!, Media.Json),
            JobState.Failed => ApiProblems.Failed(),
            _ => ApiProblems.Pending()
        };
    }
}
