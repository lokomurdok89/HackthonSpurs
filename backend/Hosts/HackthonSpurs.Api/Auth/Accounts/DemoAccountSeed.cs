using HackthonSpurs.Application.Commands;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth.Accounts;

internal static class DemoAccountSeed
{
    /// <summary>Atomically insert missing demo identities without rewriting any existing account or session.</summary>
    /// <param name="db">Migrated workspace database, shared with the account maintenance API.</param>
    /// <param name="hasher">Workspace password hasher used only for new accounts.</param>
    /// <param name="settings">Private initial credentials from the API host.</param>
    /// <param name="ct">Startup cancellation.</param>
    internal static async Task ApplyAsync(DocumentsDb db, IPasswordHasher<WorkspaceUser> hasher,
        IConfiguration settings, CancellationToken ct)
    {
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        var admin = ServiceDefaults.DemoAdminUsername;
        var reader = ServiceDefaults.DemoReaderUsername;
        var names = new[] { admin.ToUpperInvariant(), reader.ToUpperInvariant() };
        var existing = await db.WorkspaceUsers.AsNoTracking().Where(u => names.Contains(u.NormalizedUsername))
            .ToDictionaryAsync(u => u.NormalizedUsername, u => u.Role, ct);
        foreach (var (name, role, key) in new[] {
            (admin, Wire.AdminRole, SettingNames.DemoAdminPassword),
            (reader, Wire.ReaderRole, SettingNames.DemoReaderPassword) })
        {
            if (existing.TryGetValue(name.ToUpperInvariant(), out var found))
            {
                if (found != role) throw new InvalidOperationException(ResourceText.DemoSeedRoleConflict);
                continue;
            }

            var password = settings[key];
            if (!WorkspaceAccountInput.ValidPassword(password) ||
                settings[SettingNames.DemoAdminPassword] == settings[SettingNames.DemoReaderPassword])
                throw new InvalidOperationException(ResourceText.DemoSeedPasswordInvalid);
            db.WorkspaceUsers.Add(AccountFields.Create(name, name, password!, role, hasher));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
