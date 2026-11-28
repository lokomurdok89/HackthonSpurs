namespace HackthonSpurs.Application.Queries;

public sealed record SearchStats(bool EmbeddingsConfigured, string Model, int TotalDocuments, int IndexedDocuments, int TotalChunks);
