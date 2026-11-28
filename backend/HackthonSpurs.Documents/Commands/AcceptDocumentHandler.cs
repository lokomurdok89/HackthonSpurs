using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HackthonSpurs.Documents.Commands;

public enum Admission { Accepted, Conflict, Unsupported, TooLarge }

/// <summary>Return the admission decision and the accepted document ID, if any.</summary>
/// <param name="Outcome">Admission or replay decision.</param>
/// <param name="Id">Opaque document identifier.</param>
public readonly record struct Receipt(Admission Outcome, Guid Id = default);

/// <summary>Request admission of one staged upload under an idempotency key.</summary>
/// <param name="Key">Client operation key reused for retries.</param>
/// <param name="File">Bounded staged original.</param>
/// <param name="DeclaredType">Media type of the file part.</param>
/// <param name="Spur">Whether external image reading was explicitly requested.</param>
public sealed record AcceptDocument(Guid Key, StagedFile File, string DeclaredType, bool Spur);

/// <summary>Own idempotency, publication of the original and creation of the queued job.</summary>
/// <param name="db">Scoped document database context.</param>
public sealed class AcceptDocumentHandler(DocumentsDb db)
{
    /// <summary>Commit the original and job once, or replay the matching receipt.</summary>
    /// <param name="command">Upload intent and staged file metadata.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<Receipt> HandleAsync(AcceptDocument command, CancellationToken ct)
    {
        var (key, file, declaredType, spur) = command;
        var existing = await FindAsync(key, ct);
        if (existing is not null) return Compare(existing, file, spur);
        var type = FileIdentification.Identify(file.TemporaryPath, declaredType, spur);
        if (type is Media.Png or Media.Jpeg && file.Length > OcrFormats.MaxImageBytes) return new(Admission.TooLarge);
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
            SendToOcr = spur,
            ByteCount = file.Length,
            CreatedAt = DateTimeOffset.UtcNow,
            State = JobState.Queued
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: Wire.OperationIndex })
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var winner = await FindAsync(key, ct) ?? throw new InvalidOperationException(ResourceText.CommittedOperationMissing);
            return Compare(winner, file, spur);
        }

        file.Publish();
        await tx.CommitAsync(ct);
        return new(Admission.Accepted, file.Id);
    }

    /// <summary>Look up only the metadata needed to arbitrate a previously accepted key.</summary>
    /// <param name="key">Operation key used for replay detection.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    private Task<Existing?> FindAsync(Guid key, CancellationToken ct) =>
        db.Documents.AsNoTracking().Where(d => d.OperationKey == key)
          .Select(d => new Existing(d.Id, d.Sha256, d.SendToOcr)).SingleOrDefaultAsync(ct);

    /// <summary>Accept a replay only when the bytes and processing mode match.</summary>
    /// <param name="doc">Previously accepted document metadata.</param>
    /// <param name="file">Current staged upload.</param>
    /// <param name="spur">Whether the SPUR image lane is selected.</param>
    private static Receipt Compare(Existing doc, StagedFile file, bool spur) =>
        doc.Hash == file.Sha256 && spur == doc.SendToOcr ? new(Admission.Accepted, doc.Id) : new(Admission.Conflict);

    /// <summary>Capture the committed metadata needed to recognize a replay.</summary>
    /// <param name="Id">Opaque document identifier.</param>
    /// <param name="Hash">Expected SHA-256 of the original.</param>
    /// <param name="SendToOcr">Original processing mode.</param>
    private sealed record Existing(Guid Id, string Hash, bool SendToOcr);
}
