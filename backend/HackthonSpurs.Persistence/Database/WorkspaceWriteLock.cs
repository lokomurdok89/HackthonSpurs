using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HackthonSpurs.Documents.Persistence;

public static class WorkspaceWriteLock
{
    /// <summary>Serialize workspace writers across API instances without blocking plain reads.</summary>
    /// <param name="db">Request-scoped shared PostgreSQL context.</param>
    /// <param name="ct">Operation cancellation.</param>
    public static async Task<IDbContextTransaction> AcquireAsync(DocumentsDb db, CancellationToken ct)
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
