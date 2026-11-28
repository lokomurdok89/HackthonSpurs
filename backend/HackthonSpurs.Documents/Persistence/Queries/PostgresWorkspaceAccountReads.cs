using HackthonSpurs.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Queries;

/// <summary>Read only the workspace account summaries used by administrator listing.</summary>
/// <param name="db">Scoped workspace database.</param>
public sealed class PostgresWorkspaceAccountReads(DocumentsDb db) : IWorkspaceAccountReads
{
    /// <inheritdoc />
    public Task<UserView[]> ListAsync(CancellationToken ct) => db.WorkspaceUsers.AsNoTracking()
        .OrderBy(user => user.NormalizedUsername)
        .Select(user => new UserView(user.Id, user.Username, user.DisplayName, user.Role, user.Active))
        .ToArrayAsync(ct);
}
