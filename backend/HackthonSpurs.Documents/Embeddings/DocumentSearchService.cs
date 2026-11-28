using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Storage;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Embeddings;

public sealed record IndexDocumentResult(Guid DocumentId, int ChunkCount, string Model);

public sealed record SemanticSearchHit(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string? SourceName,
    string Locator,
    string Text,
    string Model,
    double Score);

public sealed record SearchStats(
    bool EmbeddingsConfigured,
    string Model,
    bool VisionOcrConfigured,
    string? VisionModel,
    int TotalDocuments,
    int IndexedDocuments,
    int TotalChunks);

public sealed record DocumentChunkView(
    Guid ChunkId,
    int ChunkIndex,
    string Locator,
    string Text,
    string Model);

public sealed record DocumentDetailView(
    Guid DocumentId,
    string? SourceName,
    string MediaType,
    long ByteCount,
    string State,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DocumentChunkView> Chunks);

public sealed class DocumentSearchService(
    DocumentsDb db,
    OriginalStore store,
    SpurEmbeddingClient embeddings,
    SpurVisionOcrClient visionOcr)
{
    public async Task<IndexDocumentResult?> IndexDocumentAsync(
        Guid documentId,
        string? sourceName,
        IReadOnlyList<ExtractedChunk>? explicitChunks,
        CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null)
            return null;

        var chunks = explicitChunks is { Count: > 0 }
            ? explicitChunks
            : await DocumentTextExtractor.ExtractAsync(store.GetOriginalPath(documentId), doc.MediaType, visionOcr, ct);

        var cleaned = chunks
            .Where(c => !string.IsNullOrWhiteSpace(c.Text))
            .Select(c => new ExtractedChunk(
                string.IsNullOrWhiteSpace(c.Locator) ? "document" : c.Locator.Trim(),
                c.Text.Trim()))
            .ToList();

        if (cleaned.Count == 0)
            return new IndexDocumentResult(documentId, 0, embeddings.Model);

        var vectors = await embeddings.EmbedBatchAsync(cleaned.Select(c => c.Text).ToList(), ct);
        var now = DateTimeOffset.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Chunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(ct);

        for (var i = 0; i < cleaned.Count; i++)
        {
            db.Chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = i,
                SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim(),
                Locator = cleaned[i].Locator,
                Text = cleaned[i].Text,
                Model = embeddings.Model,
                Embedding = vectors[i],
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new IndexDocumentResult(documentId, cleaned.Count, embeddings.Model);
    }

    public async Task<IReadOnlyList<SemanticSearchHit>> SearchAsync(
        string query,
        int topK,
        Guid? documentId,
        CancellationToken ct)
    {
        var clampedTopK = Math.Clamp(topK, 1, 50);
        var queryVector = await embeddings.EmbedAsync(query.Trim(), ct);

        var baseQuery = db.Chunks.AsNoTracking();
        if (documentId.HasValue)
            baseQuery = baseQuery.Where(c => c.DocumentId == documentId.Value);

        var rows = await baseQuery
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.ChunkIndex,
                c.SourceName,
                c.Locator,
                c.Text,
                c.Model,
                Distance = c.Embedding.CosineDistance(queryVector)
            })
            .OrderBy(r => r.Distance)
            .Take(clampedTopK)
            .ToListAsync(ct);

        return rows.Select(r => new SemanticSearchHit(
            r.Id,
            r.DocumentId,
            r.ChunkIndex,
            r.SourceName,
            r.Locator,
            r.Text,
            r.Model,
            Math.Round(Math.Clamp(1.0 - r.Distance, 0.0, 1.0), 4)
        )).ToList();
    }

    public async Task<SearchStats> GetStatsAsync(CancellationToken ct)
    {
        var totalDocuments = await db.Documents.CountAsync(ct);
        var totalChunks = await db.Chunks.CountAsync(ct);
        var indexedDocuments = await db.Chunks.Select(c => c.DocumentId).Distinct().CountAsync(ct);

        return new SearchStats(
            embeddings.IsConfigured,
            embeddings.Model,
            visionOcr.IsConfigured,
            visionOcr.IsConfigured ? visionOcr.Model : null,
            totalDocuments,
            indexedDocuments,
            totalChunks);
    }

    public async Task<DocumentDetailView?> GetDocumentDetailAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null)
            return null;

        var chunks = await db.Chunks.AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new { c.Id, c.ChunkIndex, c.SourceName, c.Locator, c.Text, c.Model })
            .ToListAsync(ct);

        var sourceName = chunks.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.SourceName))?.SourceName;
        return new DocumentDetailView(
            doc.Id,
            sourceName,
            doc.MediaType,
            doc.ByteCount,
            doc.State.ToString(),
            doc.CreatedAt,
            chunks.Select(c => new DocumentChunkView(c.Id, c.ChunkIndex, c.Locator, c.Text, c.Model)).ToList());
    }
}
