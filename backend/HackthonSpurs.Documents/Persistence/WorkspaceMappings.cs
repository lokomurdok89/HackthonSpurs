using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HackthonSpurs.Documents.Persistence;

public static class WorkspaceMappings
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

    /// <summary>Serialize workspace writers across API instances without blocking plain reads.</summary>
    /// <param name="db">Request-scoped shared PostgreSQL context.</param>
    /// <param name="ct">Operation cancellation.</param>
    public static async Task<IDbContextTransaction> LockWritersAsync(DocumentsDb db, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE workspace_users IN SHARE ROW EXCLUSIVE MODE", ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
