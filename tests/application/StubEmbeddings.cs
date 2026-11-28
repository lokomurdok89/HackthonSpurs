using HackthonSpurs.Application.Embeddings;

namespace HackthonSpurs.Application.Tests;

internal sealed class StubEmbeddings : ITextEmbeddings
{
    public int Calls { get; private set; }
    public IReadOnlyList<string>? Inputs { get; private set; }
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        Calls++;
        Inputs = texts;
        ct.ThrowIfCancellationRequested();
        return Failure is { } ex
            ? Task.FromException<IReadOnlyList<ReadOnlyMemory<float>>>(ex)
            : Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(texts.Select(_ => new ReadOnlyMemory<float>([1f, 2f])).ToArray());
    }
}
