using HackthonSpurs.Application.Commands;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Documents.Persistence.Queries;
using HackthonSpurs.Domain.Workspace;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Commands;

/// <summary>Serialize workspace account mutations and session revocation under the existing database lock.</summary>
/// <param name="db">Request-scoped workspace persistence.</param>
/// <param name="hasher">ASP.NET Core password hasher for new and reset passwords.</param>
/// <param name="clock">Time source for revalidating the acting session.</param>
public sealed class PostgresWorkspaceAccountWriter(DocumentsDb db, IPasswordHasher<WorkspaceUser> hasher,
    TimeProvider clock) : IWorkspaceAccountWriter
{
    /// <inheritdoc />
    public async Task<UserChange> CreateAsync(string? digest, string username, string displayName, string password, string role, CancellationToken ct)
    {
        var user = AccountFields.Create(username, displayName, password, role, hasher);
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var caller = await FindLockedCallerAsync(digest, ct);
        if (CallerError(caller) is { } denied) return new(Error: denied);
        if (await db.WorkspaceUsers.AnyAsync(u => u.NormalizedUsername == user.NormalizedUsername, ct))
            return new(Error: Errors.UsernameTaken);
        db.WorkspaceUsers.Add(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(User: AccountFields.Public(user));
    }

    /// <inheritdoc />
    public async Task<UserChange> UpdateAsync(string? digest, Guid id, string? displayName, string? role, bool? active,
        string? password, CancellationToken ct)
    {
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var caller = await FindLockedCallerAsync(digest, ct);
        if (CallerError(caller) is { } denied) return new(Error: denied);
        var user = await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return new(Error: Errors.UserNotFound);

        var newRole = role ?? user.Role;
        var newActive = active ?? user.Active;
        var self = caller!.Id == id;
        var removesAccess = AdministratorPolicy.RemovesAccess(user is { Active: true, Role: Wire.AdminRole },
            newActive && newRole == Wire.AdminRole);
        var otherAdministrator = removesAccess && await db.WorkspaceUsers
            .AnyAsync(u => u.Id != id && u.Active && u.Role == Wire.AdminRole, ct);
        var transition = AdministratorPolicy.Decide(removesAccess, otherAdministrator, self);
        if (transition == AdministratorTransition.LastActiveAdministrator) return new(Error: Errors.LastActiveAdmin);
        if (transition == AdministratorTransition.SelfLockout) return new(Error: Errors.SelfLockout);

        var revoke = newRole != user.Role || newActive != user.Active || password is not null;
        user.DisplayName = displayName ?? user.DisplayName;
        user.Role = newRole;
        user.Active = newActive;
        if (password is not null) user.PasswordHash = hasher.HashPassword(user, password);
        if (revoke) await db.WorkspaceSessions.Where(s => s.UserId == id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(User: AccountFields.Public(user), CallerRevoked: revoke && self);
    }

    /// <summary>Revalidate the acting session on this context so the check uses the transaction holding the writer lock.</summary>
    /// <param name="digest">Lookup digest for the acting session, if present.</param>
    /// <param name="ct">Request cancellation.</param>
    private Task<UserView?> FindLockedCallerAsync(string? digest, CancellationToken ct) =>
        PostgresWorkspaceSessionLookup.FindUserAsync(db, clock, digest, ct);

    /// <summary>Determine whether the just-resolved session still grants administrator access.</summary>
    /// <param name="caller">Active session account, if present.</param>
    private static BackendError? CallerError(UserView? caller) => caller is null ? Errors.AuthenticationRequired
        : caller.Role != Wire.AdminRole ? Errors.Forbidden : null;
}
