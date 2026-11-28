namespace HackthonSpurs.Application.Queries;

public interface IWorkspaceSessionLookup
{
    /// <summary>Find the active account for an unexpired session without changing state or returning session material.</summary>
    /// <param name="digest">SHA-256 of a well-formed opaque session token, if present.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<UserView?> FindUserAsync(string? digest, CancellationToken ct);
}
