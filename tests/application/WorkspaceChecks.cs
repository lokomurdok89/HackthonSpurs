using HackthonSpurs.Application.Commands;
using HackthonSpurs.Domain.Workspace;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Tests;

internal static class WorkspaceChecks
{
    /// <summary>Exercise normalized account commands and the standalone administrator policy.</summary>
    public static async Task RunAsync()
    {
        var accounts = new Writer();
        var commands = new WorkspaceAccountCommands(accounts);
        string password = " a bounded synthetic value ";
        Check.Require((await commands.CreateAsync("digest", "!invalid", "Name", password, Wire.AdminRole, default))
            .Error == Errors.InvalidAuthRequest && accounts.Create is null, "Invalid names must not reach the write boundary.");
        var created = await commands.CreateAsync("digest", " Example.Admin ", " Example Admin ", password, Wire.AdminRole, default);
        Check.Require(created.Error is null && accounts.Create == ("digest", "Example.Admin", "Example Admin", password, Wire.AdminRole),
            "Normalize login and display fields without altering payload passwords, role or session lookup.");
        var id = Guid.NewGuid();
        Check.Require((await commands.UpdateAsync("digest", id, null, null, null, null, default)).Error ==
            Errors.InvalidAuthRequest && accounts.Update is null, "An empty patch must not enter a write transaction.");
        await commands.UpdateAsync("digest", id, " New Name ", null, false, null, default);
        Check.Require(accounts.Update == ("digest", id, "New Name", (string?)null, (bool?)false, (string?)null),
            "A patch must preserve the distinction between absent fields and false activation.");

        Check.Require(AdministratorPolicy.RemovesAccess(true, false) && !AdministratorPolicy.RemovesAccess(true, true) &&
            !AdministratorPolicy.RemovesAccess(false, false), "Only an active administrator losing access is a removal.");
        Check.Require(AdministratorPolicy.Decide(true, false, true) == AdministratorTransition.LastActiveAdministrator &&
            AdministratorPolicy.Decide(true, true, true) == AdministratorTransition.SelfLockout &&
            AdministratorPolicy.Decide(true, true, false) == AdministratorTransition.Allowed &&
            AdministratorPolicy.Decide(false, false, true) == AdministratorTransition.Allowed,
            "Last administrator, self-lockout and harmless or authorized transitions must remain distinct.");
    }

    private sealed class Writer : IWorkspaceAccountWriter
    {
        public (string? Digest, string Username, string DisplayName, string Password, string Role)? Create { get; private set; }
        public (string? Digest, Guid Id, string? DisplayName, string? Role, bool? Active, string? Password)? Update { get; private set; }

        /// <inheritdoc />
        public Task<UserChange> CreateAsync(string? digest, string username, string displayName, string password,
            string role, CancellationToken ct)
        {
            Create = (digest, username, displayName, password, role);
            return Task.FromResult(new UserChange());
        }

        /// <inheritdoc />
        public Task<UserChange> UpdateAsync(string? digest, Guid id, string? displayName, string? role, bool? active,
            string? password, CancellationToken ct)
        {
            Update = (digest, id, displayName, role, active, password);
            return Task.FromResult(new UserChange());
        }
    }
}
