namespace HackthonSpurs.Application.Queries;

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
