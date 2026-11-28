using HackthonSpurs.Api.Http;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Endpoints.Documents;

internal static class DocumentReadEndpoints
{
    /// <summary>Map the status projection to HTTP, omitting failure details for nonfailed jobs.</summary>
    /// <param name="id">Requested document UUID.</param>
    /// <param name="reads">Document progress and publication projections.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Status(string id, IDocumentReads reads, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await reads.GetStatusAsync(key, ct);
        if (row is null) return ApiProblems.NotFound();
        return row.Failed
            ? Results.Ok(new { id = key, state = row.State, row.Attempt, row.FailureCode })
            : Results.Ok(new { id = key, state = row.State, row.Attempt });
    }

    /// <summary>Map the persisted reading or job state to the public result response.</summary>
    /// <param name="id">Requested document UUID.</param>
    /// <param name="reads">Document progress and publication projections.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public static async Task<IResult> Result(string id, IDocumentReads reads, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await reads.GetResultAsync(key, ct);
        return row?.Availability switch
        {
            null => ApiProblems.NotFound(),
            ReadingAvailability.Published => Results.Text(row.ResultJson!, Media.Json),
            ReadingAvailability.Failed => ApiProblems.Failed(),
            _ => ApiProblems.Pending()
        };
    }
}
