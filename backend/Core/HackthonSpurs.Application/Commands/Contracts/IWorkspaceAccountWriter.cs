namespace HackthonSpurs.Application.Commands;

public interface IWorkspaceAccountWriter
{
    /// <summary>Create an active account, rechecking the acting session under the write lock.</summary>
    /// <param name="digest">Opaque lookup digest for the acting session.</param>
    /// <param name="username">Validated account login name.</param>
    /// <param name="displayName">Validated public account name.</param>
    /// <param name="password">Unmodified, validated password to hash.</param>
    /// <param name="role">Validated workspace role.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<UserChange> CreateAsync(string? digest, string username, string displayName, string password, string role, CancellationToken ct);

    /// <summary>Apply the normalized changes under the writer lock, enforcing invariants and revoking affected sessions.</summary>
    /// <param name="digest">Opaque lookup digest for the acting session.</param>
    /// <param name="id">Account selected for change.</param>
    /// <param name="displayName">Replacement display name, or null to preserve.</param>
    /// <param name="role">Replacement role, or null to preserve.</param>
    /// <param name="active">Replacement activation state, or null to preserve.</param>
    /// <param name="password">Replacement password to hash, or null to preserve.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<UserChange> UpdateAsync(string? digest, Guid id, string? displayName, string? role, bool? active, string? password, CancellationToken ct);
}
