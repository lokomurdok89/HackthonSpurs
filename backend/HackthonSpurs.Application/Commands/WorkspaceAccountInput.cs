using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Commands;

public static class WorkspaceAccountInput
{
    /// <summary>Trim and validate an ASCII login name, retaining its original case.</summary>
    /// <param name="value">Submitted login name.</param>
    public static string? Username(string? value)
    {
        var username = value?.Trim();
        return username is { Length: >= Policy.MinUsernameLength and <= Policy.MaxUsernameLength } &&
            char.IsAsciiLetterOrDigit(username[0]) &&
            username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') ? username : null;
    }

    /// <summary>Trim a display name and require printable, bounded text.</summary>
    /// <param name="value">Submitted display name.</param>
    public static string? DisplayName(string? value)
    {
        var displayName = value?.Trim();
        return displayName is { Length: > 0 and <= Policy.MaxDisplayNameLength } &&
            !displayName.Any(char.IsControl) ? displayName : null;
    }

    /// <summary>Check a password's length and content without trimming it.</summary>
    /// <param name="value">Submitted password.</param>
    public static bool ValidPassword(string? value) =>
        value is { Length: >= Policy.MinPasswordLength and <= Policy.MaxPasswordLength } && !string.IsNullOrWhiteSpace(value);

    /// <summary>Recognize only an exact workspace role.</summary>
    /// <param name="value">Submitted role.</param>
    public static bool ValidRole(string? value) => value is Wire.AdminRole or Wire.ReaderRole;
}
