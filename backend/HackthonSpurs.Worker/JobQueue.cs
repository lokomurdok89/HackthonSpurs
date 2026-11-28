using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;

namespace HackthonSpurs.Worker;

internal sealed record Claim(Guid Id, Guid Token, int Attempt, long ByteCount);
internal sealed class JobQueue(NpgsqlDataSource data)
{
    internal const int AttemptLimit = 3;
    internal static readonly string FixtureHash = GetFixtureHash();
    private static string GetFixtureHash()
    {
        using var fixture = typeof(JobQueue).Assembly.GetManifestResourceStream("worker-demo.csv")!;
        return Convert.ToHexStringLower(SHA256.HashData(fixture));
    }

    // One statement: recover an expired job or take a queued one, never hold a lock during I/O.
    public async Task<Claim?> ClaimAsync(CancellationToken ct)
    {
        await using var cmd = data.CreateCommand("""
            WITH candidate AS (
              SELECT "Id" FROM document_jobs WHERE "Sha256" = $1 AND "MediaType" = 'text/csv'
              AND (("State" = 'Queued' AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now()))
                OR ("State" = 'Processing' AND "LeaseUntil" <= now()))
              ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            )
            UPDATE document_jobs j SET
              "State" = CASE WHEN "Attempt" >= $3 THEN 'Failed' ELSE 'Processing' END,
              "FailureCode" = CASE WHEN "Attempt" >= $3 THEN 'lease_expired' END,
              "LeaseToken" = CASE WHEN "Attempt" < $3 THEN $2::uuid END,
              "LeaseUntil" = CASE WHEN "Attempt" < $3 THEN now() + interval '30 seconds' END,
              "NextAttemptAt" = NULL, "Attempt" = LEAST("Attempt" + 1, $3)
            FROM candidate c WHERE j."Id" = c."Id"
            RETURNING j."Id", j."LeaseToken", j."Attempt", j."ByteCount"
            """);
        cmd.Parameters.AddWithValue(FixtureHash);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(AttemptLimit);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || r.IsDBNull(1)) return null;
        return new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetInt64(3));
    }

    public async Task<bool> FinishAsync(Claim job, ReadOutcome outcome, CancellationToken ct)
    {
        bool retry = outcome.Retryable && job.Attempt < AttemptLimit;
        await using var cmd = data.CreateCommand("""
            UPDATE document_jobs SET "State" = $3, "FailureCode" = $4, "ResultJson" = $5,
              "NextAttemptAt" = CASE WHEN $3 = 'Queued' THEN now() + $6 END,
              "LeaseToken" = NULL, "LeaseUntil" = NULL
            WHERE "Id" = $1 AND "State" = 'Processing' AND "LeaseToken" = $2 AND "LeaseUntil" > now()
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Token);
        cmd.Parameters.AddWithValue(outcome.Json != null ? "Completed" : retry ? "Queued" : "Failed");
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)(retry ? null : outcome.Error) ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = (object?)outcome.Json ?? DBNull.Value });
        cmd.Parameters.AddWithValue(TimeSpan.FromSeconds(job.Attempt == 1 ? 5 : 30));
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }
}
