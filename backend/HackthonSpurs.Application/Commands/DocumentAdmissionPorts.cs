using HackthonSpurs.Domain.Documents;

namespace HackthonSpurs.Application.Commands;

/// <summary>Expose only bounded staging metadata and operations; disposal removes unpublished staging, never the final original.</summary>
public interface IStagedOriginal : IDisposable
{
    Guid Id { get; }
    string Sha256 { get; }
    long Length { get; }

    /// <summary>Identify the actual format and report any format-specific limit violation.</summary>
    /// <param name="declaredType">Untrusted media type submitted by the caller.</param>
    /// <param name="spur">Whether external OCR formats are enabled for this upload.</param>
    OriginalInspection Inspect(string declaredType, bool spur);

    /// <summary>Move this original into durable storage without exposing its location.</summary>
    void Publish();
}

public readonly record struct OriginalInspection(string? MediaType, bool TooLarge = false);
public sealed record AcceptedSubmission(Guid Id, SubmissionIdentity Identity);
public sealed record PendingAdmission(Guid Key, Guid Id, SubmissionIdentity Identity, string MediaType, long Length);

public interface IDocumentAdmissions
{
    /// <summary>Find the committed receipt and identity for the supplied operation key, if present.</summary>
    /// <param name="key">Client operation key.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<AcceptedSubmission?> FindAsync(Guid key, CancellationToken ct);

    /// <summary>Reserve the unique key, publish the original, and commit one queued job; return the competing committed submission without publishing.</summary>
    /// <param name="pending">Validated original metadata for a new job.</param>
    /// <param name="publishOriginal">Operation to invoke only after successfully reserving the key and before committing.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<AcceptedSubmission?> CommitAsync(PendingAdmission pending, Action publishOriginal, CancellationToken ct);
}
