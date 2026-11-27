using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api;

internal static class DocumentQueries
{
    public static async Task<IResult> Status(string id, DocumentsDb db, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await db.Documents.AsNoTracking().Where(d => d.Id == key)
            .Select(d => new { d.State, d.Attempt, d.FailureCode }).SingleOrDefaultAsync(ct);
        if (row is null) return ApiProblems.NotFound();
        var state = row.State.ToString();
        return row.State == JobState.Failed
            ? Results.Ok(new { id = key, state, row.Attempt, row.FailureCode })
            : Results.Ok(new { id = key, state, row.Attempt });
    }

    public static async Task<IResult> Result(string id, DocumentsDb db, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.InvalidId();
        var row = await db.Documents.AsNoTracking().Where(d => d.Id == key)
            .Select(d => new { d.State, d.ResultJson }).SingleOrDefaultAsync(ct);
        return row?.State switch
        {
            null => ApiProblems.NotFound(),
            JobState.Completed => Results.Text(row.ResultJson!, "application/json"),
            JobState.Failed => ApiProblems.Failed(),
            _ => ApiProblems.Pending()
        };
    }
}
