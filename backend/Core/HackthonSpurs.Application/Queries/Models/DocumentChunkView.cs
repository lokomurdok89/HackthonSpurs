namespace HackthonSpurs.Application.Queries;

public sealed record DocumentChunkView(Guid ChunkId, int ChunkIndex, string Locator, string Text, string Model);
