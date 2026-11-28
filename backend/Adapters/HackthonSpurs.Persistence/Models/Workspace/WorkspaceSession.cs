namespace HackthonSpurs.Documents.Persistence;

public sealed class WorkspaceSession
{
    public required string Digest { get; set; }
    public Guid UserId { get; set; }
    public WorkspaceUser User { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
}
