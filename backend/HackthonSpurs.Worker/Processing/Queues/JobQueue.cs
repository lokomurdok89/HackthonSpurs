using HackthonSpurs.Worker.Ocr.Contracts;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;

namespace HackthonSpurs.Worker.Processing.Queues;

/// <summary>Describe ownership of a job and the expected original for this attempt.</summary>
/// <param name="Id">Opaque document identifier.</param>
/// <param name="Token">Current lease ownership token.</param>
/// <param name="Attempt">Number of claims made.</param>
/// <param name="ByteCount">Expected original length.</param>
/// <param name="MediaType">Verified media type.</param>
/// <param name="Hash">Expected SHA-256 of the original.</param>
internal sealed record Claim(Guid Id, Guid Token, int Attempt, long ByteCount, string MediaType, string Hash);
/// <summary>Apply atomic lease and publication operations for one processing lane.</summary>
/// <param name="data">PostgreSQL connection source.</param>
/// <param name="spur">Whether the SPUR image lane is selected.</param>
internal sealed class JobQueue(NpgsqlDataSource data, bool spur)
{
    internal const int AttemptLimit = Policy.MaxAttempts;
    internal static readonly string FixtureHash = GetFixtureHash();
    /// <summary>Compute the allowlisted hash from the embedded synthetic fixture.</summary>
    private static string GetFixtureHash()
    {
        using var fixture = typeof(JobQueue).Assembly.GetManifestResourceStream(Wire.FixtureResource)!;
        return Convert.ToHexStringLower(SHA256.HashData(fixture));
    }

    // One statement: recover an expired job or take a queued one, never hold a lock during I/O.
    /// <summary>Acquire an eligible job or fail a final expired lease using database time.</summary>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<Claim?> ClaimAsync(CancellationToken ct)
    {
        await using var cmd = data.CreateCommand("""
            WITH candidate AS (
              SELECT "Id" FROM document_jobs WHERE "SendToOcr" = $4
                AND ($4 OR ("Sha256" = $1 AND "MediaType" = $8))
              AND (("State" = 'Queued' AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= now()))
                OR ("State" = 'Processing' AND "LeaseUntil" <= now()))
              ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            )
            UPDATE document_jobs j SET
              "State" = CASE WHEN "Attempt" >= $3 THEN 'Failed' ELSE 'Processing' END,
              "FailureCode" = CASE WHEN "Attempt" >= $3 THEN $5::text END,
              "LeaseToken" = CASE WHEN "Attempt" < $3 THEN $2::uuid END,
              "LeaseUntil" = CASE WHEN "Attempt" < $3 THEN now() + CASE WHEN $4 THEN $6::interval ELSE $7::interval END END,
              "NextAttemptAt" = NULL, "Attempt" = LEAST("Attempt" + 1, $3)
            FROM candidate c WHERE j."Id" = c."Id"
            RETURNING j."Id", j."LeaseToken", j."Attempt", j."ByteCount", j."MediaType", j."Sha256"
            """);
        cmd.Parameters.AddWithValue(FixtureHash);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(spur ? Policy.OcrAttempts : AttemptLimit);
        cmd.Parameters.AddWithValue(spur);
        cmd.Parameters.AddWithValue(Errors.LeaseExpired.Code);
        cmd.Parameters.AddWithValue(TimeSpan.FromMinutes(Policy.LeaseMinutes));
        cmd.Parameters.AddWithValue(TimeSpan.FromSeconds(Policy.FixtureLeaseSeconds));
        cmd.Parameters.AddWithValue(Media.Csv);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || r.IsDBNull(1)) return null;
        return new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetInt64(3), r.GetString(4), r.GetString(5));
    }

    /// <summary>Publish a result, retry or fail only while the claim still owns its lease.</summary>
    /// <param name="job">Claim authorizing this attempt.</param>
    /// <param name="outcome">Validated reading or classified failure.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<bool> FinishAsync(Claim job, ReadOutcome outcome, CancellationToken ct)
    {
        bool retry = !spur && outcome.Retryable && job.Attempt < AttemptLimit;
        await using var cmd = data.CreateCommand("""
            WITH finished AS (
              UPDATE document_jobs SET "State" = $3, "FailureCode" = $4, "ResultJson" = $5,
                "NextAttemptAt" = CASE WHEN $3 = 'Queued' THEN now() + $6 END,
                "LeaseToken" = NULL, "LeaseUntil" = NULL
              WHERE "Id" = $1 AND "State" = 'Processing' AND "LeaseToken" = $2 AND "LeaseUntil" > now()
              RETURNING "Id", "SendToOcr", "ResultJson"
            ), queued AS (
              INSERT INTO document_index_jobs ("DocumentId", "State", "CreatedAt", "Attempt", "ChunkCount")
              SELECT "Id", CASE WHEN jsonb_array_length("ResultJson"->'fragments') > 0 THEN 'Queued' ELSE 'Skipped' END,
                now(), 0, 0 FROM finished WHERE "SendToOcr" AND "ResultJson" IS NOT NULL
              ON CONFLICT ("DocumentId") DO NOTHING
            )
            SELECT EXISTS (SELECT 1 FROM finished)
            """);
        cmd.Parameters.AddWithValue(job.Id);
        cmd.Parameters.AddWithValue(job.Token);
        cmd.Parameters.AddWithValue(outcome.Json != null ? nameof(JobState.Completed) : retry ? nameof(JobState.Queued) : nameof(JobState.Failed));
        cmd.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Text,
            Value = (object?)(retry ? null : outcome.Error) ?? DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = (object?)outcome.Json ?? DBNull.Value });
        cmd.Parameters.AddWithValue(TimeSpan.FromSeconds(job.Attempt == 1 ? Policy.FirstBackoffSeconds : Policy.SecondBackoffSeconds));
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}
