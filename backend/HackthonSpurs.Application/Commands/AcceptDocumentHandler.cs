using HackthonSpurs.Domain.Documents;

namespace HackthonSpurs.Application.Commands;

public enum Admission { Accepted, Conflict, Unsupported, TooLarge }

/// <summary>Return an admission decision and, on acceptance, the opaque receipt ID.</summary>
/// <param name="Outcome">Admission or replay decision.</param>
/// <param name="Id">Accepted receipt, if any.</param>
public readonly record struct Receipt(Admission Outcome, Guid Id = default);

/// <summary>Request admission of one bounded original under an idempotency key.</summary>
/// <param name="Key">Client operation key reused for retries.</param>
/// <param name="Original">Staged upload, independent of how its bytes are stored.</param>
/// <param name="DeclaredType">Media type declared by the caller.</param>
/// <param name="Spur">Whether external image reading was explicitly requested.</param>
public sealed record AcceptDocument(Guid Key, IStagedOriginal Original, string DeclaredType, bool Spur);

/// <summary>Own validation and idempotency without depending on a filesystem or database provider.</summary>
/// <param name="admissions">Durable acceptance store that resolves competing submissions.</param>
public sealed class AcceptDocumentHandler(IDocumentAdmissions admissions)
{
    /// <summary>Check replay first, then inspect and publish a new original only with its queued job.</summary>
    /// <param name="command">Staged upload and admission intent.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<Receipt> HandleAsync(AcceptDocument command, CancellationToken ct)
    {
        var (key, original, declaredType, spur) = command;
        var proposed = new SubmissionIdentity(original.Sha256, spur);
        var existing = await admissions.FindAsync(key, ct);
        if (existing is not null) return Compare(existing, proposed);

        var inspection = original.Inspect(declaredType, spur);
        if (inspection.TooLarge) return new(Admission.TooLarge);
        if (inspection.MediaType is null) return new(Admission.Unsupported);
        var pending = new PendingAdmission(key, original.Id, proposed, inspection.MediaType, original.Length);
        var winner = await admissions.CommitAsync(pending, original.Publish, ct);
        return winner is null ? new(Admission.Accepted, original.Id) : Compare(winner, proposed);
    }

    /// <summary>Accept an exact replay of a committed operation, otherwise report a conflict.</summary>
    /// <param name="existing">Document previously committed under the same operation key.</param>
    /// <param name="proposed">Current content digest and processing mode.</param>
    private static Receipt Compare(AcceptedSubmission existing, SubmissionIdentity proposed) =>
        existing.Identity.Matches(proposed) ? new(Admission.Accepted, existing.Id) : new(Admission.Conflict);
}
