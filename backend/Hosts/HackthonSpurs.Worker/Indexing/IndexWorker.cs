using HackthonSpurs.Application.Processing;
using HackthonSpurs.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HackthonSpurs.Worker.Indexing;

/// <summary>Poll the optional index lane without access to original files or the OCR reader.</summary>
/// <param name="indexing">Per-attempt index use case.</param>
/// <param name="log">Safe log for worker outcomes.</param>
internal sealed class IndexWorker(ProcessIndexJob indexing, ILogger<IndexWorker> log) : BackgroundService
{
    /// <summary>Process durable index jobs until shutdown; leave ambiguous writes to lease recovery.</summary>
    /// <param name="stopping">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (await indexing.RunOnceAsync(stopping) is { } attempt)
                {
                    if (log.IsEnabled(LogLevel.Information))
                        log.LogInformation(ResourceText.IndexLog, attempt.Id, attempt.Attempt, attempt.Outcome);
                    continue;
                }
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                log.LogWarning(ResourceText.IndexUnavailable);
            }
            await Task.Delay(TimeSpan.FromSeconds(Policy.PollSeconds), stopping);
        }
    }
}
