namespace HackthonSpurs.Application.Queries;

public interface IWorkspaceAccountReads
{
    /// <summary>List workspace account summaries ordered by normalized login name.</summary>
    /// <param name="ct">Request cancellation.</param>
    Task<UserView[]> ListAsync(CancellationToken ct);
}
