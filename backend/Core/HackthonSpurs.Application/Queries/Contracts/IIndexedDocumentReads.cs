namespace HackthonSpurs.Application.Queries;

public interface IIndexedDocumentReads
{
    /// <summary>Read document metadata and compatible chunks without touching originals or vectors.</summary>
    /// <param name="id">Receipt to inspect.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<DocumentDetailView?> GetDocumentDetailAsync(Guid id, CancellationToken ct);

    /// <summary>Count observable documents and current indexes without embedding any text.</summary>
    /// <param name="ct">Request cancellation.</param>
    Task<SearchStats> GetStatsAsync(CancellationToken ct);
}
