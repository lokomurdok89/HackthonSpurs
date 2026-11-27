using HackthonSpurs.Documents.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HackthonSpurs.Documents.Persistence;

public enum Admission { Accepted, Conflict, Unsupported }
public readonly record struct Receipt(Admission Outcome, Guid Id = default);

public sealed class Ingestion(DocumentsDb db)
{
    public async Task<Receipt> AcceptAsync(Guid key, StagedFile file, string declaredType, CancellationToken ct)
    {
        var existing = await FindAsync(key, ct);
        if (existing is not null) return Compare(existing, file);
        var type = FileIdentification.Identify(file.TemporaryPath, declaredType);
        if (type is null) return new(Admission.Unsupported);

        // Reserve the key before publishing the original. PostgreSQL serializes
        // contenders on the unique index, including across different API instances.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Documents.Add(new DocumentJob
        {
            Id = file.Id,
            OperationKey = key,
            Sha256 = file.Sha256,
            MediaType = type,
            ByteCount = file.Length,
            CreatedAt = DateTimeOffset.UtcNow,
            State = JobState.Queued
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_document_operation" })
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var winner = await FindAsync(key, ct) ?? throw new InvalidOperationException("Missing committed operation.");
            return Compare(winner, file);
        }

        file.Publish();
        await tx.CommitAsync(ct);
        return new(Admission.Accepted, file.Id);
    }

    private Task<Existing?> FindAsync(Guid key, CancellationToken ct) =>
        db.Documents.AsNoTracking().Where(d => d.OperationKey == key)
          .Select(d => new Existing(d.Id, d.Sha256)).SingleOrDefaultAsync(ct);

    private static Receipt Compare(Existing doc, StagedFile file) =>
        doc.Hash == file.Sha256 ? new(Admission.Accepted, doc.Id) : new(Admission.Conflict);

    private sealed record Existing(Guid Id, string Hash);
}
