namespace HackthonSpurs.Application.Queries;

public sealed record DocumentChunkView(Guid ChunkId, int ChunkIndex, string Locator, string Text, string Model);
public sealed record DocumentDetailView(Guid DocumentId, string? SourceName, string MediaType, long ByteCount, string State,
    DateTimeOffset CreatedAt, IReadOnlyList<DocumentChunkView> Chunks);
public sealed record SearchPageLocator(string Kind, int Page);
public sealed record SemanticSearchHit(Guid ChunkId, Guid DocumentId, int ChunkIndex, string? SourceName, string Locator,
    SearchPageLocator PageLocator, string Text, string Model, double Score);
public sealed record SearchStats(bool EmbeddingsConfigured, string Model, int TotalDocuments, int IndexedDocuments, int TotalChunks);
public sealed record SearchResultView(string Model, IReadOnlyList<SemanticSearchHit> Results);

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

public interface ISemanticSearchIndex
{
    /// <summary>Check whether compatible published content exists for the requested scope.</summary>
    /// <param name="documentId">Optional document receipt restricting the query.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<bool> HasCompatibleChunksAsync(Guid? documentId, CancellationToken ct);

    /// <summary>Rank published excerpts in the active vector space.</summary>
    /// <param name="vector">Embedding for the explicit query.</param>
    /// <param name="topK">Maximum number of hits.</param>
    /// <param name="documentId">Optional document receipt restricting the query.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<IReadOnlyList<SemanticSearchHit>> RankAsync(ReadOnlyMemory<float> vector, int topK, Guid? documentId, CancellationToken ct);
}
