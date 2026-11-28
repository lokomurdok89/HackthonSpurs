using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Commands;

/// <summary>Validate administrator account commands separately from account projections.</summary>
/// <param name="writer">Serialized mutation boundary for the shared workspace.</param>
public sealed class WorkspaceAccountCommands(IWorkspaceAccountWriter writer)
{
    /// <summary>Normalize a new account request and delegate the authorized mutation.</summary>
    /// <param name="digest">Opaque lookup digest for the acting session.</param>
    /// <param name="username">Untrusted account login name.</param>
    /// <param name="displayName">Untrusted account display name.</param>
    /// <param name="password">Untrusted password, not altered by validation.</param>
    /// <param name="role">Untrusted requested workspace role.</param>
    /// <param name="ct">Request cancellation.</param>
    public Task<UserChange> CreateAsync(string? digest, string? username, string? displayName,
        string? password, string? role, CancellationToken ct)
    {
        var name = WorkspaceAccountInput.Username(username);
        var display = WorkspaceAccountInput.DisplayName(displayName);
        return name is null || display is null || !WorkspaceAccountInput.ValidPassword(password) ||
            !WorkspaceAccountInput.ValidRole(role) ? Invalid() : writer.CreateAsync(digest, name, display, password!, role!, ct);
    }

    /// <summary>Reject an empty or invalid update before entering the serialized mutation boundary.</summary>
    /// <param name="digest">Opaque lookup digest for the acting session.</param>
    /// <param name="id">Account selected for change.</param>
    /// <param name="displayName">Untrusted display name, or null to preserve.</param>
    /// <param name="role">Untrusted role, or null to preserve.</param>
    /// <param name="active">Optional activation state.</param>
    /// <param name="password">Untrusted password, or null to preserve.</param>
    /// <param name="ct">Request cancellation.</param>
    public Task<UserChange> UpdateAsync(string? digest, Guid id, string? displayName, string? role, bool? active,
        string? password, CancellationToken ct)
    {
        var display = WorkspaceAccountInput.DisplayName(displayName);
        return (displayName is null && role is null && active is null && password is null) ||
            (displayName is not null && display is null) || (role is not null && !WorkspaceAccountInput.ValidRole(role)) ||
            (password is not null && !WorkspaceAccountInput.ValidPassword(password))
            ? Invalid() : writer.UpdateAsync(digest, id, display, role, active, password, ct);
    }

    /// <summary>Return the catalog failure for invalid command fields.</summary>
    private static Task<UserChange> Invalid() => Task.FromResult(new UserChange(Error: Errors.InvalidAuthRequest));
}
