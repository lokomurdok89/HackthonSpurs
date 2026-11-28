namespace HackthonSpurs.Documents.Persistence;

public sealed class WorkspaceUser
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string NormalizedUsername { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public bool Active { get; set; }
}
