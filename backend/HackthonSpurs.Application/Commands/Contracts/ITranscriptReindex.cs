namespace HackthonSpurs.Application.Commands;

public interface ITranscriptReindex
{
    /// <summary>Atomically reset an eligible terminal index, or coalesce a request with work already in progress.</summary>
    /// <param name="id">Document receipt selected by the administrator.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<ReindexOutcome> RequeueAsync(Guid id, CancellationToken ct);
}
