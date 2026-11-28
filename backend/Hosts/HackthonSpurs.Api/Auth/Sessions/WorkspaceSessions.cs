using HackthonSpurs.Api.Contracts;
using HackthonSpurs.Application.Commands;
using HackthonSpurs.Application.Queries;
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

/// <summary>Carry an issued token to the cookie adapter without including it in a generated string representation.</summary>
/// <param name="user">Public account summary for the response.</param>
/// <param name="token">Opaque, unlogged token used only to issue the cookie.</param>
/// <param name="expiresAt">Fixed expiry applied to the session and cookie.</param>
internal sealed class SignedInSession(UserView user, string token, DateTimeOffset expiresAt)
{
    public UserView User { get; } = user;
    public string Token { get; } = token;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
}

/// <summary>Issue and revoke server-side workspace sessions.</summary>
/// <param name="db">Request-scoped workspace and documents persistence.</param>
/// <param name="hasher">Framework password hasher used for account verification.</param>
/// <param name="dummy">Precomputed credential for unknown users.</param>
/// <param name="clock">Time source for fixed session expiry.</param>
internal sealed class WorkspaceSessions(DocumentsDb db, IPasswordHasher<WorkspaceUser> hasher,
    DummyCredential dummy, TimeProvider clock)
{
    /// <summary>Verify credentials and replace the previous session, returning the newly issued token.</summary>
    /// <param name="previous">Digest of the existing session, if present.</param>
    /// <param name="input">Untrusted, syntactically bounded sign-in payload.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<SignedInSession?> SignInAsync(string? previous, LoginRequest input, CancellationToken ct)
    {
        var username = WorkspaceAccountInput.Username(input.Username);
        if (username is null || !WorkspaceAccountInput.ValidPassword(input.Password)) return null;
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
        await db.WorkspaceSessions.Where(s => s.ExpiresAt <= now || (previous != null && s.Digest == previous))
            .ExecuteDeleteAsync(ct);
        db.WorkspaceSessions.Add(new WorkspaceSession { Digest = SessionTokens.Digest(token), UserId = user.Id, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(AccountFields.Public(user), token, expires);
    }

    /// <summary>Delete the selected session without extending any other sessions.</summary>
    /// <param name="digest">Digest of the session to revoke, if present.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task SignOutAsync(string? digest, CancellationToken ct)
    {
        if (digest is not null)
            await db.WorkspaceSessions.Where(s => s.Digest == digest).ExecuteDeleteAsync(ct);
    }
}
