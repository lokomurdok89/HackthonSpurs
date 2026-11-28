using HackthonSpurs.Application.Queries;
using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth.Sessions;

/// <summary>Resolve opaque sessions for the HTTP adapter without issuing or updating a session.</summary>
/// <param name="db">Request-scoped workspace persistence.</param>
/// <param name="clock">Time source for fixed session expiry.</param>
internal sealed class WorkspaceSessionLookup(DocumentsDb db, TimeProvider clock)
{
    /// <summary>Resolve a digest to an active user without exposing any session material.</summary>
    /// <param name="digest">SHA-256 of a well-formed opaque cookie if present.</param>
    /// <param name="ct">Request cancellation.</param>
    public Task<UserView?> FindUserAsync(string? digest, CancellationToken ct)
    {
        if (digest is null) return Task.FromResult<UserView?>(null);
        var now = clock.GetUtcNow();
        return db.WorkspaceSessions.AsNoTracking()
            .Where(s => s.Digest == digest && s.ExpiresAt > now && s.User.Active)
            .Select(s => new UserView(s.UserId, s.User.Username, s.User.DisplayName, s.User.Role, s.User.Active))
            .SingleOrDefaultAsync(ct);
    }
}
