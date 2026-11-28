using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Queries;

public sealed record DocumentChunkView(Guid ChunkId, int ChunkIndex, string Locator, string Text, string Model);
public sealed record DocumentDetailView(Guid DocumentId, string? SourceName, string MediaType, long ByteCount, string State,
    DateTimeOffset CreatedAt, IReadOnlyList<DocumentChunkView> Chunks);
public sealed record SearchPageLocator(string Kind, int Page);
public sealed record SemanticSearchHit(Guid ChunkId, Guid DocumentId, int ChunkIndex, string? SourceName, string Locator, SearchPageLocator PageLocator,
    string Text, string Model, double Score);
public sealed record SearchStats(bool EmbeddingsConfigured, string Model, int TotalDocuments, int IndexedDocuments, int TotalChunks);

/// <summary>Search completed OCR indexes in the currently configured vector space, without mutating documents.</summary>
/// <param name="db">Scoped document database context.</param>
/// <param name="embeddings">Provider adapter used only to embed the explicit query.</param>
/// <param name="profile">Expected provider model, vector revision and dimensions.</param>
public sealed class DocumentSearchService(DocumentsDb db, SpurEmbeddingClient embeddings, SpurEmbeddingSettings profile)
{
    /// <summary>Rank page excerpts by cosine similarity, optionally for one receipt.</summary>
    /// <param name="query">Bounded nonempty query text, sent to SPUR only if compatible data is indexed.</param>
    /// <param name="topK">Maximum number of hits, already validated by the API.</param>
    /// <param name="documentId">Optional document scope.</param>
    /// <param name="ct">Cancellation for the explicit search.</param>
    public async Task<IReadOnlyList<SemanticSearchHit>> SearchAsync(string query, int topK, Guid? documentId, CancellationToken ct)
    {
        if (!profile.IsConfigured) throw new SpurEmbeddingException(Errors.EmbeddingsNotConfigured);
        var chunks = CompatibleChunks();
        if (documentId.HasValue) chunks = chunks.Where(c => c.DocumentId == documentId.Value);
        if (!await chunks.AnyAsync(ct)) return [];

        var queryVector = await embeddings.EmbedAsync(query, ct);
        var rows = await chunks
            .Select(c => new { c.Id, c.DocumentId, c.ChunkIndex, c.Locator, c.Page, c.Text, c.Model, Distance = c.Embedding.CosineDistance(queryVector) })
            .OrderBy(r => r.Distance).ThenBy(r => r.DocumentId).ThenBy(r => r.ChunkIndex)
            .Take(topK).ToListAsync(ct);

        return rows.Select(r => new SemanticSearchHit(r.Id, r.DocumentId, r.ChunkIndex, null, r.Locator, new(Wire.Page, r.Page!.Value),
            r.Text, r.Model, Math.Round(Math.Clamp(1.0 - r.Distance, 0.0, 1.0), Policy.ScoreDecimals))).ToList();
    }

    /// <summary>Read document metadata and compatible chunks without loading original files or vector values.</summary>
    /// <param name="id">Receipt to inspect.</param>
    /// <param name="ct">Request cancellation.</param>
    public async Task<DocumentDetailView?> GetDocumentDetailAsync(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.Id, d.MediaType, d.ByteCount, d.State, d.CreatedAt }).SingleOrDefaultAsync(ct);
        if (doc is null) return null;
        var chunks = await CompatibleChunks().Where(c => c.DocumentId == id).OrderBy(c => c.ChunkIndex)
            .Select(c => new DocumentChunkView(c.Id, c.ChunkIndex, c.Locator, c.Text, c.Model)).ToListAsync(ct);
        return new(doc.Id, null, doc.MediaType, doc.ByteCount, doc.State.ToString(), doc.CreatedAt, chunks);
    }

    /// <summary>Count observable documents and current indexes without calling the embedding provider.</summary>
    /// <param name="ct">Cancellation for the read-only database queries.</param>
    public async Task<SearchStats> GetStatsAsync(CancellationToken ct)
    {
        var totalDocuments = await db.Documents.CountAsync(ct);
        if (!profile.IsConfigured) return new(false, profile.ModelId, totalDocuments, 0, 0);
        var chunks = CompatibleChunks();
        int totalChunks = await chunks.CountAsync(ct);
        int indexedDocuments = await chunks.Select(c => c.DocumentId).Distinct().CountAsync(ct);
        return new(true, profile.ModelId, totalDocuments, indexedDocuments, totalChunks);
    }

    /// <summary>Exclude legacy chunks, incomplete work, local uploads and incompatible vector spaces.</summary>
    private IQueryable<DocumentChunk> CompatibleChunks() =>
        from c in db.Chunks.AsNoTracking()
        join i in db.IndexJobs.AsNoTracking() on c.DocumentId equals i.DocumentId
        join d in db.Documents.AsNoTracking() on c.DocumentId equals d.Id
        where d.SendToOcr && d.State == JobState.Completed &&
            i.State == IndexState.Completed && i.EmbeddingVersion == profile.ProfileVersion &&
            i.Dimensions == profile.VectorDimensions && c.Model == profile.ModelId && c.Page != null
        select c;
}
