namespace HackthonSpurs.Application.Queries;

public sealed record DocumentDetailView(Guid DocumentId, string? SourceName, string MediaType, long ByteCount, string State,
    DateTimeOffset CreatedAt, IReadOnlyList<DocumentChunkView> Chunks);
