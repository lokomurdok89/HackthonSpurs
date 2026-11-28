using HackthonSpurs.Application.Commands;
using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Application.Tests;

internal static class SearchChecks
{
    /// <summary>Verify disabled and empty search avoid the provider, and compatible work uses scoped ranking.</summary>
    public static async Task RunAsync()
    {
        var index = new SearchIndex();
        var embeddings = new StubEmbeddings();
        var profile = new EmbeddingProfile(false, "synthetic", "v1", 2);
        var id = Guid.NewGuid();

        var disabled = new DocumentSearchService(index, embeddings, profile);
        try
        {
            await disabled.SearchAsync("explicit question", 3, id, default);
            throw new InvalidOperationException("Disabled search should fail explicitly.");
        }
        catch (EmbeddingFailure failure) when (failure.Error == Errors.EmbeddingsNotConfigured) { }
        Check.Require(index.Checks == 0 && embeddings.Calls == 0, "Disabled search must not touch the index or provider.");
        var reindex = new ReindexStub();
        var disabledReindex = new ReindexTranscriptHandler(reindex, profile);
        Check.Require(await disabledReindex.HandleAsync(id, default) == ReindexOutcome.Disabled && reindex.Calls == 0,
            "Disabled reindex must not mutate the index.");
        profile = profile with { IsConfigured = true };
        var enabledReindex = new ReindexTranscriptHandler(reindex, profile);
        Check.Require(await enabledReindex.HandleAsync(id, default) == ReindexOutcome.Ineligible && reindex.Calls == 1,
            "The reindex handler must preserve the atomic adapter's eligibility decision.");

        var enabled = new DocumentSearchService(index, embeddings, profile);
        var empty = await enabled.SearchAsync("explicit question", 3, id, default);
        Check.Require(empty.Model == profile.ModelId && empty.Results.Count == 0 && index.Checks == 1 && embeddings.Calls == 0,
            "A scope without compatible chunks must not send user text to the provider.");
        index.Compatible = true;
        var result = await enabled.SearchAsync("explicit question", 3, id, default);
        Check.Require(index.CheckedId == id && index.RankInput is { } rank && rank.Id == id && rank.TopK == 3 &&
            rank.Vector.Span.SequenceEqual([1f, 2f]) && result.Results is [var hit] && hit.DocumentId == id &&
            embeddings.Inputs!.SequenceEqual(["explicit question"]), "Explicit search must pass the validated scope and provider vector to ranking.");
    }

    private sealed class SearchIndex : ISemanticSearchIndex
    {
        public bool Compatible { get; set; }
        public int Checks { get; private set; }
        public Guid? CheckedId { get; private set; }
        public (ReadOnlyMemory<float> Vector, int TopK, Guid? Id)? RankInput { get; private set; }

        /// <inheritdoc />
        public Task<bool> HasCompatibleChunksAsync(Guid? documentId, CancellationToken ct)
        {
            Checks++;
            CheckedId = documentId;
            return Task.FromResult(Compatible);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SemanticSearchHit>> RankAsync(ReadOnlyMemory<float> vector, int topK, Guid? documentId, CancellationToken ct)
        {
            RankInput = (vector, topK, documentId);
            return Task.FromResult<IReadOnlyList<SemanticSearchHit>>(
                [new(Guid.NewGuid(), documentId!.Value, 0, null, "page 1", new("page", 1), "synthetic", "model", 0.8)]);
        }
    }

    private sealed class ReindexStub : ITranscriptReindex
    {
        public int Calls { get; private set; }

        /// <inheritdoc />
        public Task<ReindexOutcome> RequeueAsync(Guid id, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(ReindexOutcome.Ineligible);
        }
    }
}
