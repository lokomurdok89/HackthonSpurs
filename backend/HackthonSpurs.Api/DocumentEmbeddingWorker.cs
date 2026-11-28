using System.Threading.Channels;
using HackthonSpurs.Documents.Embeddings;

namespace HackthonSpurs.Api;

internal sealed record EmbeddingWorkItem(Guid DocumentId, string? SourceName);

internal sealed class EmbeddingWorkQueue
{
    private readonly Channel<EmbeddingWorkItem> _channel = Channel.CreateUnbounded<EmbeddingWorkItem>();

    public void Enqueue(Guid documentId, string? sourceName) =>
        _channel.Writer.TryWrite(new EmbeddingWorkItem(documentId, sourceName));

    public IAsyncEnumerable<EmbeddingWorkItem> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

internal sealed class DocumentEmbeddingWorker(
    EmbeddingWorkQueue queue,
    IServiceScopeFactory scopes,
    SpurEmbeddingSettings settings,
    ILogger<DocumentEmbeddingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            if (!settings.IsConfigured)
                continue;

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var search = scope.ServiceProvider.GetRequiredService<DocumentSearchService>();
                var result = await search.IndexDocumentAsync(item.DocumentId, item.SourceName, null, stoppingToken);
                if (result is not null)
                {
                    logger.LogInformation(
                        "Indexed {ChunkCount} chunks for document {DocumentId} with model {Model}.",
                        result.ChunkCount,
                        result.DocumentId,
                        result.Model);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background embedding failed for document {DocumentId}.", item.DocumentId);
            }
        }
    }
}
