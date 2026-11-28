using HackthonSpurs.Application.Processing;
using HackthonSpurs.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HackthonSpurs.Worker.Processing;

/// <summary>Poll the active reading lane and log safe outcomes.</summary>
/// <param name="reading">Per-attempt reading use case.</param>
/// <param name="log">Logger for operational outcomes.</param>
internal sealed class DocumentWorker(ProcessReadingJob reading, ILogger<DocumentWorker> log) : BackgroundService
{
    /// <summary>Process eligible claims until shutdown, backing off when the queue is unavailable.</summary>
    /// <param name="stopping">Host shutdown token.</param>
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (await reading.RunOnceAsync(stopping) is { } attempt)
                {
                    if (log.IsEnabled(LogLevel.Information))
                        log.LogInformation(ResourceText.JobLog, attempt.Id, attempt.Attempt, attempt.Outcome);
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
