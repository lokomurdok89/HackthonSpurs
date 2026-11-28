using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Mappings;

internal static class WorkspaceMappings
{
    /// <summary>Configure workspace users, case-insensitive identity and revocable sessions.</summary>
    /// <param name="model">Shared PostgreSQL model builder.</param>
    internal static void Configure(ModelBuilder model)
    {
        var users = model.Entity<WorkspaceUser>();
        users.ToTable("workspace_users", table => table.HasCheckConstraint(
            "ck_workspace_role", $"\"Role\" IN ('{Wire.AdminRole}', '{Wire.ReaderRole}')"));
        users.HasKey(user => user.Id);
        users.Property(user => user.Id).ValueGeneratedNever();
        users.Property(user => user.Username).HasMaxLength(Policy.MaxUsernameLength);
        users.Property(user => user.NormalizedUsername).HasMaxLength(Policy.MaxUsernameLength);
        users.Property(user => user.DisplayName).HasMaxLength(Policy.MaxDisplayNameLength);
        users.HasIndex(user => user.NormalizedUsername).IsUnique();

        var sessions = model.Entity<WorkspaceSession>();
        sessions.ToTable("workspace_sessions");
        sessions.HasKey(session => session.Digest);
        sessions.Property(session => session.Digest).HasMaxLength(64);
        sessions.HasIndex(session => session.ExpiresAt);
        sessions.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId);
    }
}
