namespace HackthonSpurs.Application.Queries;

public interface IDocumentReads
{
    /// <summary>Project progress without loading originals or result JSON.</summary>
    /// <param name="id">Opaque document receipt.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<DocumentStatusView?> GetStatusAsync(Guid id, CancellationToken ct);

    /// <summary>Read the publication state and stored result without claiming work.</summary>
    /// <param name="id">Opaque document receipt.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<DocumentResultView?> GetResultAsync(Guid id, CancellationToken ct);

    /// <summary>Project independent index state without loading text or vector values.</summary>
    /// <param name="id">Opaque document receipt.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<DocumentIndexStatusView?> GetIndexStatusAsync(Guid id, CancellationToken ct);
}
