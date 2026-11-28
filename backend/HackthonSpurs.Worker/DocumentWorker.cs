using HackthonSpurs.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HackthonSpurs.Worker;

/// <summary>Coordinate claiming, reading and publishing jobs for one reader lane.</summary>
/// <param name="queue">Lease-aware document job store.</param>
/// <param name="reader">Client for the configured private reader.</param>
/// <param name="log">Logger for operational outcomes.</param>
internal sealed class DocumentWorker(JobQueue queue, ReaderClient reader, ILogger<DocumentWorker> log) : BackgroundService
{
    /// <summary>Process eligible claims until shutdown, backing off when the queue is unavailable.</summary>
    /// <param name="stopping">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (await queue.ClaimAsync(stopping) is { } job)
                {
                    var outcome = await reader.ReadAsync(job, stopping);
                    var saved = await queue.FinishAsync(job, outcome, stopping);
                    if (log.IsEnabled(LogLevel.Information))
                        log.LogInformation(ResourceText.JobLog, job.Id, job.Attempt,
                            saved ? outcome.Error ?? Wire.Published : Wire.LeaseLost);
                    continue;
                }
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                // Includes startup before API migrations and ambiguous writes on a connection loss.
                log.LogWarning(ResourceText.QueueUnavailable);
            }
            await Task.Delay(TimeSpan.FromSeconds(Policy.PollSeconds), stopping);
        }
    }
}
