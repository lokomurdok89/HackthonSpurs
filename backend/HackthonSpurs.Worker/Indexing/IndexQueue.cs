using HackthonSpurs.Resources;
using System.Globalization;
using HackthonSpurs.Documents.Embeddings;
using Npgsql;
using Pgvector;

namespace HackthonSpurs.Worker.Indexing;

internal sealed record IndexClaim(Guid Id, Guid Token, int Attempt, string Reading);

/// <summary>Claim OCR index work and publish one vector generation transactionally.</summary>
/// <param name="data">PostgreSQL connection source.</param>
internal sealed class IndexQueue(NpgsqlDataSource data)
{
    internal const int AttemptLimit = Policy.MaxAttempts;

    /// <summary>Claim an eligible index job or fail its final expired lease.</summary>
    /// <param name="ct">Cancellation for the lease operation.</param>
    internal async Task<IndexClaim?> ClaimAsync(CancellationToken ct)
    {
        await using var cmd = data.CreateCommand("""
            WITH candidate AS (
              SELECT i."DocumentId" FROM document_index_jobs i
              JOIN document_jobs d ON d."Id" = i."DocumentId"
              WHERE d."SendToOcr" AND d."State" = 'Completed'
                AND ((i."State" = 'Queued' AND (i."NextAttemptAt" IS NULL OR i."NextAttemptAt" <= now()))
                  OR (i."State" = 'Processing' AND i."LeaseUntil" <= now()))
              ORDER BY i."CreatedAt", i."DocumentId" LIMIT 1 FOR UPDATE OF i SKIP LOCKED
            )
            UPDATE document_index_jobs i SET
              "State" = CASE WHEN i."Attempt" >= $2 THEN 'Failed' ELSE 'Processing' END,
              "FailureCode" = CASE WHEN i."Attempt" >= $2 THEN $3::text END,
              "LeaseToken" = CASE WHEN i."Attempt" < $2 THEN $1::uuid END,
              "LeaseUntil" = CASE WHEN i."Attempt" < $2 THEN now() + $4::interval END,
              "NextAttemptAt" = NULL, "Attempt" = LEAST(i."Attempt" + 1, $2)
            FROM candidate c JOIN document_jobs d ON d."Id" = c."DocumentId" WHERE i."DocumentId" = c."DocumentId"
            RETURNING i."DocumentId", i."LeaseToken", i."Attempt", d."ResultJson"
            """);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(AttemptLimit);
        cmd.Parameters.AddWithValue(Errors.IndexLeaseExpired.Code);
        cmd.Parameters.AddWithValue(TimeSpan.FromMinutes(Policy.LeaseMinutes));
        await using var rows = await cmd.ExecuteReaderAsync(ct);
        if (!await rows.ReadAsync(ct) || rows.IsDBNull(1)) return null;
        return new(rows.GetGuid(0), rows.GetGuid(1), rows.GetInt32(2), rows.GetString(3));
    }

    /// <summary>Apply a classified failure only while the current attempt owns its lease.</summary>
    /// <param name="claim">Attempt ownership and current attempt number.</param>
    /// <param name="code">Safe error identifier for terminal failures.</param>
    /// <param name="retryable">Whether to schedule a bounded delayed retry.</param>
    /// <param name="ct">Cancellation for the database operation.</param>
    internal async Task<bool> FailAsync(IndexClaim claim, string code, bool retryable, CancellationToken ct)
    {
        await using var cmd = data.CreateCommand("""
            UPDATE document_index_jobs SET "State" = CASE WHEN $3 THEN 'Queued' ELSE 'Failed' END,
              "FailureCode" = CASE WHEN NOT $3 THEN $4::text END,
              "NextAttemptAt" = CASE WHEN $3 THEN now() + $5::interval END,
              "LeaseToken" = NULL, "LeaseUntil" = NULL
            WHERE "DocumentId" = $1 AND "LeaseToken" = $2 AND "LeaseUntil" > clock_timestamp() AND "State" = 'Processing'
            """);
        cmd.Parameters.AddWithValue(claim.Id);
        cmd.Parameters.AddWithValue(claim.Token);
        cmd.Parameters.AddWithValue(retryable && claim.Attempt < AttemptLimit);
        cmd.Parameters.AddWithValue(code);
        cmd.Parameters.AddWithValue(TimeSpan.FromSeconds(claim.Attempt == 1 ? Policy.FirstBackoffSeconds : Policy.SecondBackoffSeconds));
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>Replace all chunks and finish indexing atomically, but only for the current unexpired lease.</summary>
    /// <param name="claim">Attempt ownership for this document.</param>
    /// <param name="chunks">Ordered excerpts from the validated reading, grouped by page.</param>
    /// <param name="vectors">Validated vectors in exactly the same order as the excerpts.</param>
    /// <param name="profile">Model identity, revision and dimensions that produced the vectors.</param>
    /// <param name="ct">Cancellation for the database operation.</param>
    internal async Task<bool> PublishAsync(IndexClaim claim, IReadOnlyList<OcrIndexChunk> chunks,
        IReadOnlyList<Vector> vectors, SpurEmbeddingSettings profile, CancellationToken ct)
    {
        if (chunks.Count != vectors.Count || chunks.Count == 0) throw new ArgumentException(ResourceText.InvalidVectorBatch, nameof(vectors));
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var finish = new NpgsqlCommand("""
            UPDATE document_index_jobs SET "State" = 'Completed', "ChunkCount" = $3,
              "EmbeddingVersion" = $4, "Dimensions" = $5, "LeaseToken" = NULL, "LeaseUntil" = NULL
            WHERE "DocumentId" = $1 AND "LeaseToken" = $2 AND "LeaseUntil" > clock_timestamp() AND "State" = 'Processing'
            """, connection, transaction);
        finish.Parameters.AddWithValue(claim.Id);
        finish.Parameters.AddWithValue(claim.Token);
        finish.Parameters.AddWithValue(chunks.Count);
        finish.Parameters.AddWithValue(profile.ProfileVersion);
        finish.Parameters.AddWithValue(profile.VectorDimensions);
        if (await finish.ExecuteNonQueryAsync(ct) != 1) return false;

        await using var delete = new NpgsqlCommand("""DELETE FROM document_chunks WHERE "DocumentId" = $1""", connection, transaction);
        delete.Parameters.AddWithValue(claim.Id);
        await delete.ExecuteNonQueryAsync(ct);

        // This pool also handles startup before the vector extension is available.
        // Bind strings and cast in PostgreSQL rather than relying on that pool's type discovery.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO document_chunks
              ("Id", "DocumentId", "ChunkIndex", "Locator", "Page", "Text", "Model", "Embedding", "CreatedAt")
            SELECT gen_random_uuid(), $1, (n - 1)::int, $6::text || ' ' || p, p, t, $2, v::vector, now()
            FROM unnest($3::int[], $4::text[], $5::text[]) WITH ORDINALITY AS chunks(p, t, v, n)
            """, connection, transaction);
        insert.Parameters.AddWithValue(claim.Id);
        insert.Parameters.AddWithValue(profile.ModelId);
        insert.Parameters.AddWithValue(chunks.Select(c => c.Page).ToArray());
        insert.Parameters.AddWithValue(chunks.Select(c => c.Text).ToArray());
        insert.Parameters.AddWithValue(vectors.Select(VectorLiteral).ToArray());
        insert.Parameters.AddWithValue(Wire.Page);
        await insert.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    /// <summary>Format a validated vector for the server-side vector cast using invariant numeric syntax.</summary>
    /// <param name="vector">Finite vector verified by the embedding adapter.</param>
    private static string VectorLiteral(Vector vector) =>
        "[" + string.Join(',', vector.ToArray().Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
}
