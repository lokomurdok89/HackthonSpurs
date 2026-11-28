using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Queries;

/// <summary>Embed only explicit queries whose scope has compatible indexed excerpts.</summary>
/// <param name="index">Read-only compatible index projection and ranking.</param>
/// <param name="embeddings">Boundary for ordered text embeddings.</param>
/// <param name="profile">Current embedding availability and profile.</param>
public sealed class DocumentSearchService(ISemanticSearchIndex index, ITextEmbeddings embeddings, EmbeddingProfile profile)
{
    /// <summary>Rank page excerpts without changing persisted state.</summary>
    /// <param name="query">Bounded nonempty query text.</param>
    /// <param name="topK">Maximum number of hits.</param>
    /// <param name="documentId">Optional scope restricting the query to a single document.</param>
    /// <param name="ct">Cancellation for the explicit search.</param>
    public async Task<SearchResultView> SearchAsync(string query, int topK, Guid? documentId, CancellationToken ct)
    {
        if (!profile.IsConfigured) throw new EmbeddingFailure(Errors.EmbeddingsNotConfigured);
        if (!await index.HasCompatibleChunksAsync(documentId, ct)) return new(profile.ModelId, []);
        var vector = (await embeddings.EmbedBatchAsync([query], ct))[0];
        return new(profile.ModelId, await index.RankAsync(vector, topK, documentId, ct));
    }
}
