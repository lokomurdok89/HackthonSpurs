using Pgvector;

namespace HackthonSpurs.Documents.Persistence;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? SourceName { get; set; }
    public required string Locator { get; set; }
    public int? Page { get; set; }
    public required string Text { get; set; }
    public required string Model { get; set; }
    public required Vector Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
