using HackthonSpurs.Api.Auth.Accounts;
using HackthonSpurs.Api.Contracts;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth.Sessions;

/// <summary>Equalize password verification work when a supplied username has no account.</summary>
/// <param name="hasher">Framework hasher configured with the workspace cost.</param>
internal sealed class DummyCredential(IPasswordHasher<WorkspaceUser> hasher)
{
    public WorkspaceUser User { get; } =
        AccountFields.Create(string.Empty, string.Empty, SessionTokens.Create(), Wire.ReaderRole, hasher);
}

/// <summary>Issue, resolve and revoke server-side workspace sessions.</summary>
/// <param name="db">Request-scoped workspace and documents persistence.</param>
/// <param name="hasher">Framework password hasher used for account verification.</param>
/// <param name="dummy">Precomputed credential for unknown users.</param>
/// <param name="clock">Time source for fixed session expiry.</param>
internal sealed class WorkspaceSessions(DocumentsDb db, IPasswordHasher<WorkspaceUser> hasher,
    DummyCredential dummy, TimeProvider clock)
{
    /// <summary>Resolve the supplied digest to an active user without exposing session data.</summary>
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

    /// <summary>Verify the supplied login, replace any previous cookie and return the newly authenticated user.</summary>
    /// <param name="context">HTTP request and response for the cookie exchange.</param>
    /// <param name="input">Untrusted, syntactically bounded sign-in payload.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<UserView?> SignInAsync(HttpContext context, LoginRequest input, CancellationToken ct)
    {
        var username = AccountFields.Username(input.Username);
        if (username is null || !AccountFields.ValidPassword(input.Password)) return null;
        var normalized = username.ToUpperInvariant();
        var candidate = await db.WorkspaceUsers.AsNoTracking().SingleOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);
        var credential = candidate ?? dummy.User;
        var verification = hasher.VerifyHashedPassword(credential, credential.PasswordHash, input.Password!);
        if (candidate is null || !candidate.Active || verification == PasswordVerificationResult.Failed) return null;

        // Do not allow a successful password check against a pre-reset account to mint a token after the reset commits.
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var user = await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Id == candidate.Id, ct);
        if (user is null || !user.Active || user.Role != candidate.Role || user.PasswordHash != candidate.PasswordHash) return null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, input.Password!);

        var token = SessionTokens.Create();
        var now = clock.GetUtcNow();
        var expires = now.AddHours(Policy.SessionHours);
        var previous = SessionTokens.Read(context.Request);
        await db.WorkspaceSessions.Where(s => s.ExpiresAt <= now || (previous != null && s.Digest == previous))
            .ExecuteDeleteAsync(ct);
        db.WorkspaceSessions.Add(new WorkspaceSession { Digest = SessionTokens.Digest(token), UserId = user.Id, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        SessionTokens.Issue(context, token, expires);
        return AccountFields.Public(user);
    }

    /// <summary>Delete the current request's session and clear its cookie without extending other sessions.</summary>
    /// <param name="context">HTTP request and response for the cookie exchange.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task SignOutAsync(HttpContext context, CancellationToken ct)
    {
        if (SessionTokens.Read(context.Request) is { } digest)
            await db.WorkspaceSessions.Where(s => s.Digest == digest).ExecuteDeleteAsync(ct);
        SessionTokens.Clear(context);
    }
}
