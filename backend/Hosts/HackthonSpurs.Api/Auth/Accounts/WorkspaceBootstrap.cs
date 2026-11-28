using HackthonSpurs.Application.Commands;
using HackthonSpurs.Api.Auth.Sessions;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth.Accounts;

internal static class WorkspaceBootstrap
{
    /// <summary>Provision missing demo accounts when enabled; otherwise bootstrap the first admin on an empty workspace.</summary>
    /// <param name="services">Scope containing the migrated context and password hasher.</param>
    /// <param name="configuration">Server-only configuration with optional bootstrap values.</param>
    /// <param name="ct">Application startup cancellation.</param>
    public static async Task BootstrapAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct)
    {
        _ = services.GetRequiredService<DummyCredential>();
        if (configuration.GetValue<bool>(SettingNames.DemoSeedEnabled))
        {
            await DemoAccountSeed.ApplyAsync(services.GetRequiredService<DocumentsDb>(),
                services.GetRequiredService<IPasswordHasher<WorkspaceUser>>(), configuration, ct);
            return;
        }

        var configuredUsername = configuration[SettingNames.BootstrapUsername];
        var password = configuration[SettingNames.BootstrapPassword];
        if (string.IsNullOrEmpty(configuredUsername) && string.IsNullOrEmpty(password)) return;
        var db = services.GetRequiredService<DocumentsDb>();
        await using var transaction = await WorkspaceWriteLock.AcquireAsync(db, ct);
        if (await db.WorkspaceUsers.AnyAsync(ct)) return;
        var username = WorkspaceAccountInput.Username(configuredUsername);
        if (username is null || !WorkspaceAccountInput.ValidPassword(password))
            throw new InvalidOperationException(ResourceText.InvalidBootstrap);
        var hasher = services.GetRequiredService<IPasswordHasher<WorkspaceUser>>();
        db.WorkspaceUsers.Add(AccountFields.Create(username, username, password!, Wire.AdminRole, hasher));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
