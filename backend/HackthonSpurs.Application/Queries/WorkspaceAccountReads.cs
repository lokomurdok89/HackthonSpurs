namespace HackthonSpurs.Application.Queries;

/// <summary>Expose an account summary without any credential or session material.</summary>
public sealed record UserView(Guid Id, string Username, string DisplayName, string Role, bool Active);

public interface IWorkspaceAccountReads
{
    /// <summary>List workspace account summaries ordered by normalized login name.</summary>
    /// <param name="ct">Request cancellation.</param>
    Task<UserView[]> ListAsync(CancellationToken ct);
}
