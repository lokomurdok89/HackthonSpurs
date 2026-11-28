namespace HackthonSpurs.Application.Queries;

public sealed record SearchResultView(string Model, IReadOnlyList<SemanticSearchHit> Results);
