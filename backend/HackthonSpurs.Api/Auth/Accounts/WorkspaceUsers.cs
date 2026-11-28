using HackthonSpurs.Api.Auth.Sessions;
using HackthonSpurs.Api.Contracts;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth.Accounts;

internal sealed record UserChange(UserView? User = null, BackendError? Error = null, bool CallerRevoked = false);

/// <summary>Manage shared workspace users and preserve its active administrator invariant.</summary>
/// <param name="db">Request-scoped workspace and documents persistence.</param>
/// <param name="hasher">Framework hasher for new and reset passwords.</param>
/// <param name="sessions">Current server-side session lookup.</param>
internal sealed class WorkspaceUsers(DocumentsDb db, IPasswordHasher<WorkspaceUser> hasher, WorkspaceSessions sessions)
{
    /// <summary>List account summaries ordered by login name, respecting request cancellation.</summary>
    public async Task<UserView[]> ListAsync(CancellationToken ct) => await db.WorkspaceUsers.AsNoTracking()
        .OrderBy(user => user.NormalizedUsername)
        .Select(user => new UserView(user.Id, user.Username, user.DisplayName, user.Role, user.Active))
        .ToArrayAsync(ct);

    /// <summary>Validate and create an active user within the serialized workspace mutation boundary.</summary>
    /// <param name="digest">Digest identifying the acting administrator's current session.</param>
    /// <param name="input">Untrusted account fields.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<UserChange> CreateAsync(string? digest, CreateUserRequest input, CancellationToken ct)
    {
        var username = AccountFields.Username(input.Username);
        var displayName = AccountFields.DisplayName(input.DisplayName);
        if (username is null || displayName is null || !AccountFields.ValidPassword(input.Password) ||
            !AccountFields.ValidRole(input.Role)) return new(Error: Errors.InvalidAuthRequest);
        var user = AccountFields.Create(username, displayName, input.Password!, input.Role!, hasher);
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var caller = await sessions.FindUserAsync(digest, ct);
        if (CallerError(caller) is { } denied) return new(Error: denied);
        if (await db.WorkspaceUsers.AnyAsync(u => u.NormalizedUsername == user.NormalizedUsername, ct))
            return new(Error: Errors.UsernameTaken);
        db.WorkspaceUsers.Add(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(User: AccountFields.Public(user));
    }

    /// <summary>Apply selected changes, atomically enforcing admin invariants and revoking obsolete sessions.</summary>
    /// <param name="digest">Digest identifying the acting administrator's current session.</param>
    /// <param name="id">Opaque identifier of the account to update.</param>
    /// <param name="input">Untrusted optional account fields.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<UserChange> UpdateAsync(string? digest, Guid id, UpdateUserRequest input, CancellationToken ct)
    {
        var displayName = AccountFields.DisplayName(input.DisplayName);
        if ((input.DisplayName is null && input.Role is null && input.Active is null && input.Password is null) ||
            (input.DisplayName is not null && displayName is null) ||
            (input.Role is not null && !AccountFields.ValidRole(input.Role)) ||
            (input.Password is not null && !AccountFields.ValidPassword(input.Password)))
            return new(Error: Errors.InvalidAuthRequest);

        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var caller = await sessions.FindUserAsync(digest, ct);
        if (CallerError(caller) is { } denied) return new(Error: denied);
        var user = await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return new(Error: Errors.UserNotFound);

        var newRole = input.Role ?? user.Role;
        var newActive = input.Active ?? user.Active;
        var self = caller!.Id == id;
        if (user is { Active: true, Role: Wire.AdminRole } && (!newActive || newRole != Wire.AdminRole))
        {
            if (!await db.WorkspaceUsers.AnyAsync(u => u.Id != id && u.Active && u.Role == Wire.AdminRole, ct))
                return new(Error: Errors.LastActiveAdmin);
            if (self) return new(Error: Errors.SelfLockout);
        }
        var revoke = newRole != user.Role || newActive != user.Active || input.Password is not null;
        user.DisplayName = displayName ?? user.DisplayName;
        user.Role = newRole;
        user.Active = newActive;
        if (input.Password is not null) user.PasswordHash = hasher.HashPassword(user, input.Password);
        if (revoke) await db.WorkspaceSessions.Where(s => s.UserId == id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(User: AccountFields.Public(user), CallerRevoked: revoke && self);
    }

    /// <summary>Determine whether the just-resolved session still grants administrator access.</summary>
    private static BackendError? CallerError(UserView? caller) => caller is null ? Errors.AuthenticationRequired
        : caller.Role != Wire.AdminRole ? Errors.Forbidden : null;
}
