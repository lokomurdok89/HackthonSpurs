using HackthonSpurs.Domain.Documents;

namespace HackthonSpurs.Application.Commands;

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
