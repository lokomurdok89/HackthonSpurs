using HackthonSpurs.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Queries;

/// <summary>Resolve workspace sessions without issuing or updating a session.</summary>
/// <param name="db">Request-scoped workspace persistence.</param>
/// <param name="clock">Time source for fixed session expiry.</param>
public sealed class PostgresWorkspaceSessionLookup(DocumentsDb db, TimeProvider clock) : IWorkspaceSessionLookup
{
    /// <inheritdoc />
    public Task<UserView?> FindUserAsync(string? digest, CancellationToken ct) => FindUserAsync(db, clock, digest, ct);

    /// <summary>Resolve the session on the supplied context, reusing its active transaction if any.</summary>
    /// <param name="db">Context owning the query and any active transaction.</param>
    /// <param name="clock">Time source for fixed session expiry.</param>
    /// <param name="digest">Lookup digest for the session, if present.</param>
    /// <param name="ct">Request cancellation.</param>
    internal static Task<UserView?> FindUserAsync(DocumentsDb db, TimeProvider clock, string? digest, CancellationToken ct)
    {
        if (digest is null) return Task.FromResult<UserView?>(null);
        var now = clock.GetUtcNow();
        return db.WorkspaceSessions.AsNoTracking()
            .Where(s => s.Digest == digest && s.ExpiresAt > now && s.User.Active)
            .Select(s => new UserView(s.UserId, s.User.Username, s.User.DisplayName, s.User.Role, s.User.Active))
            .SingleOrDefaultAsync(ct);
    }
}
