using HackthonSpurs.Application.Commands;
using HackthonSpurs.Domain.Documents;
using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HackthonSpurs.Documents.Persistence.Commands;

/// <summary>Commit a document admission using the existing unique index and a single database transaction.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class PostgresDocumentAdmissions(DocumentsDb db) : IDocumentAdmissions
{
    /// <inheritdoc />
    public Task<AcceptedSubmission?> FindAsync(Guid key, CancellationToken ct) =>
        db.Documents.AsNoTracking().Where(d => d.OperationKey == key)
            .Select(d => new AcceptedSubmission(d.Id, new SubmissionIdentity(d.Sha256, d.SendToOcr))).SingleOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<AcceptedSubmission?> CommitAsync(PendingAdmission pending, Action publishOriginal, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Documents.Add(new DocumentJob
        {
            Id = pending.Id,
            OperationKey = pending.Key,
            Sha256 = pending.Identity.Sha256,
            MediaType = pending.MediaType,
            SendToOcr = pending.Identity.SendToOcr,
            ByteCount = pending.Length,
            CreatedAt = DateTimeOffset.UtcNow,
            State = JobState.Queued
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: Wire.OperationIndex })
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await FindAsync(pending.Key, ct) ?? throw new InvalidOperationException(ResourceText.CommittedOperationMissing);
        }

        // Never compensate the move after a commit exception: the server may already have committed.
        publishOriginal();
        await tx.CommitAsync(ct);
        return null;
    }
}
