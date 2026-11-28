namespace HackthonSpurs.Application.Embeddings;

public interface ITextEmbeddings
{
    /// <summary>Compute one validated vector per bounded input, in input order.</summary>
    /// <param name="texts">Excerpts or a single explicit search query.</param>
    /// <param name="ct">Cancellation for this operation.</param>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
