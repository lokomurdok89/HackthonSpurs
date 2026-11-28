namespace HackthonSpurs.Application.Commands;

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
