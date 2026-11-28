using HackthonSpurs.Application.Queries;
using Microsoft.AspNetCore.Identity;

namespace HackthonSpurs.Documents.Persistence;

public static class AccountFields
{
    /// <summary>Create an active user from validated account fields and hash the supplied password.</summary>
    /// <param name="username">Original-cased login name.</param>
    /// <param name="displayName">Public display name within the workspace.</param>
    /// <param name="password">Unmodified password supplied by its administrator.</param>
    /// <param name="role">Authorized workspace role.</param>
    /// <param name="hasher">Framework password hasher.</param>
    public static WorkspaceUser Create(string username, string displayName, string password, string role,
        IPasswordHasher<WorkspaceUser> hasher)
    {
        var user = new WorkspaceUser
        {
            Id = Guid.NewGuid(), Username = username, NormalizedUsername = username.ToUpperInvariant(),
            DisplayName = displayName, PasswordHash = string.Empty, Role = role, Active = true
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }

    /// <summary>Project the supplied account without password or session material.</summary>
    /// <param name="user">Account to project.</param>
    public static UserView Public(WorkspaceUser user) => new(user.Id, user.Username, user.DisplayName, user.Role, user.Active);
}
