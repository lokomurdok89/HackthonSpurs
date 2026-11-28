using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Queries;

/// <summary>Project or rank current OCR excerpts using the existing PostgreSQL vector space.</summary>
/// <param name="db">Scoped document database context.</param>
/// <param name="profile">Expected model, vector revision and dimensions.</param>
public sealed class PostgresSemanticIndex(DocumentsDb db, EmbeddingProfile profile) : IIndexedDocumentReads, ISemanticSearchIndex
{
    /// <inheritdoc />
    public Task<bool> HasCompatibleChunksAsync(Guid? documentId, CancellationToken ct) => Scope(documentId).AnyAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticSearchHit>> RankAsync(ReadOnlyMemory<float> vector, int topK, Guid? documentId, CancellationToken ct)
    {
        var queryVector = new Vector(vector);
        var rows = await Scope(documentId)
            .Select(c => new { c.Id, c.DocumentId, c.ChunkIndex, c.Locator, c.Page, c.Text, c.Model, Distance = c.Embedding.CosineDistance(queryVector) })
            .OrderBy(r => r.Distance).ThenBy(r => r.DocumentId).ThenBy(r => r.ChunkIndex)
            .Take(topK).ToListAsync(ct);

        return rows.Select(r => new SemanticSearchHit(r.Id, r.DocumentId, r.ChunkIndex, null, r.Locator, new(Wire.Page, r.Page!.Value),
            r.Text, r.Model, Math.Round(Math.Clamp(1.0 - r.Distance, 0.0, 1.0), Policy.ScoreDecimals))).ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentDetailView?> GetDocumentDetailAsync(Guid id, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { d.Id, d.MediaType, d.ByteCount, d.State, d.CreatedAt }).SingleOrDefaultAsync(ct);
        if (doc is null) return null;
        var chunks = await Scope(id).OrderBy(c => c.ChunkIndex)
            .Select(c => new DocumentChunkView(c.Id, c.ChunkIndex, c.Locator, c.Text, c.Model)).ToListAsync(ct);
        return new(doc.Id, null, doc.MediaType, doc.ByteCount, doc.State.ToString(), doc.CreatedAt, chunks);
    }

    /// <inheritdoc />
    public async Task<SearchStats> GetStatsAsync(CancellationToken ct)
    {
        var totalDocuments = await db.Documents.CountAsync(ct);
        if (!profile.IsConfigured) return new(false, profile.ModelId, totalDocuments, 0, 0);
        var chunks = Scope(null);
        int totalChunks = await chunks.CountAsync(ct);
        int indexedDocuments = await chunks.Select(c => c.DocumentId).Distinct().CountAsync(ct);
        return new(true, profile.ModelId, totalDocuments, indexedDocuments, totalChunks);
    }

    /// <summary>Exclude legacy chunks, incomplete work, local uploads and incompatible vector spaces.</summary>
    /// <param name="documentId">Optional receipt filter.</param>
    private IQueryable<DocumentChunk> Scope(Guid? documentId)
    {
        var chunks = from c in db.Chunks.AsNoTracking()
                     join i in db.IndexJobs.AsNoTracking() on c.DocumentId equals i.DocumentId
                     join d in db.Documents.AsNoTracking() on c.DocumentId equals d.Id
                     where d.SendToOcr && d.State == JobState.Completed &&
                         i.State == IndexState.Completed && i.EmbeddingVersion == profile.ProfileVersion &&
                         i.Dimensions == profile.VectorDimensions && c.Model == profile.ModelId && c.Page != null
                     select c;
        return documentId.HasValue ? chunks.Where(c => c.DocumentId == documentId.Value) : chunks;
    }
}
