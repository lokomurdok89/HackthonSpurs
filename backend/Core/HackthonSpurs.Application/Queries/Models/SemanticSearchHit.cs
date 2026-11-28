namespace HackthonSpurs.Application.Queries;

public sealed record SemanticSearchHit(Guid ChunkId, Guid DocumentId, int ChunkIndex, string? SourceName, string Locator,
    SearchPageLocator PageLocator, string Text, string Model, double Score);
