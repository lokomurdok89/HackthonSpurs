using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HackthonSpurs.Worker;

internal sealed class DocumentWorker(JobQueue queue, ReaderClient reader, ILogger<DocumentWorker> log) : BackgroundService
{
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
                    log.LogInformation("Job {Id}, attempt {Attempt}: {Outcome}", job.Id, job.Attempt,
                        saved ? outcome.Error ?? "published" : "lease_lost");
                    continue;
                }
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                // Includes startup before API migrations and ambiguous writes on a connection loss.
                log.LogWarning("Queue unavailable; retrying. No result was assumed committed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stopping);
        }
    }
}
