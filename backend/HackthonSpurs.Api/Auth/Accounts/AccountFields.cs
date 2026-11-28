using HackthonSpurs.Api.Contracts;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;

namespace HackthonSpurs.Api.Auth.Accounts;

internal static class AccountFields
{
    /// <summary>Trim and validate the supplied ASCII login name, retaining its original case.</summary>
    public static string? Username(string? value)
    {
        var username = value?.Trim();
        return username is { Length: >= Policy.MinUsernameLength and <= Policy.MaxUsernameLength } &&
            char.IsAsciiLetterOrDigit(username[0]) &&
            username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') ? username : null;
    }

    /// <summary>Trim the supplied display name and require printable, bounded text.</summary>
    public static string? DisplayName(string? value)
    {
        var displayName = value?.Trim();
        return displayName is { Length: > 0 and <= Policy.MaxDisplayNameLength } &&
            !displayName.Any(char.IsControl) ? displayName : null;
    }

    /// <summary>Check the supplied password's length and content without trimming it.</summary>
    public static bool ValidPassword(string? value) =>
        value is { Length: >= Policy.MinPasswordLength and <= Policy.MaxPasswordLength } && !string.IsNullOrWhiteSpace(value);

    /// <summary>Accept the supplied role only if it is an exact workspace role.</summary>
    public static bool ValidRole(string? value) => value is Wire.AdminRole or Wire.ReaderRole;

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
    public static UserView Public(WorkspaceUser user) => new(user.Id, user.Username, user.DisplayName, user.Role, user.Active);
}
