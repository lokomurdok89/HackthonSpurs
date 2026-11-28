namespace HackthonSpurs.Application.Queries;

public sealed record DocumentIndexStatusView(string State, int Attempt, int ChunkCount, string? FailureCode);
