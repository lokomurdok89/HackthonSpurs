using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace HackthonSpurs.Documents.Persistence;

public enum IndexState { Queued, Processing, Completed, Skipped, Failed }

public sealed class DocumentIndexJob
{
    public Guid DocumentId { get; set; }
    public IndexState State { get; set; }
    public int Attempt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? FailureCode { get; set; }
    public string? EmbeddingVersion { get; set; }
    public int? Dimensions { get; set; }
    public int ChunkCount { get; set; }
}

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? SourceName { get; set; }
    public required string Locator { get; set; }
    public int? Page { get; set; }
    public required string Text { get; set; }
    public required string Model { get; set; }
    public required Vector Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class IndexMappings
{
    /// <summary>Map leased index jobs and the vector records published by successful indexes.</summary>
    /// <param name="model">EF Core model builder for the document database.</param>
    internal static void Configure(ModelBuilder model)
    {
        model.HasPostgresExtension("vector");
        var job = model.Entity<DocumentIndexJob>();
        job.ToTable("document_index_jobs", t =>
        {
            t.HasCheckConstraint("ck_index_attempt", """ "Attempt" BETWEEN 0 AND 3 """);
            t.HasCheckConstraint("ck_index_state", """ "State" IN ('Queued','Processing','Completed','Skipped','Failed') """);
            t.HasCheckConstraint("ck_index_failure", """ ("State" = 'Failed') = ("FailureCode" IS NOT NULL) """);
            t.HasCheckConstraint("ck_index_lease", """
                (("State" = 'Processing') = ("LeaseToken" IS NOT NULL)) AND (("State" = 'Processing') = ("LeaseUntil" IS NOT NULL))
                """);
            t.HasCheckConstraint("ck_index_chunks", """
                ("State" = 'Completed' AND "ChunkCount" > 0 AND "Dimensions" IS NOT NULL AND "EmbeddingVersion" IS NOT NULL)
                OR ("State" <> 'Completed' AND "ChunkCount" = 0 AND "Dimensions" IS NULL AND "EmbeddingVersion" IS NULL)
                """);
        });
        job.HasKey(j => j.DocumentId);
        job.Property(j => j.DocumentId).ValueGeneratedNever();
        job.Property(j => j.State).HasConversion<string>();
        job.HasIndex(j => new { j.State, j.CreatedAt }).HasDatabaseName("ix_document_index_queue");
        job.HasOne<DocumentJob>().WithMany().HasForeignKey(j => j.DocumentId).OnDelete(DeleteBehavior.Cascade);

        var chunk = model.Entity<DocumentChunk>();
        chunk.ToTable("document_chunks", t =>
        {
            t.HasCheckConstraint("ck_chunk_index", "\"ChunkIndex\" >= 0");
            t.HasCheckConstraint("ck_chunk_page", """ "Page" IS NULL OR "Page" BETWEEN 1 AND 20 """);
        });
        chunk.HasKey(c => c.Id);
        chunk.Property(c => c.Id).ValueGeneratedNever();
        chunk.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique().HasDatabaseName("ux_document_chunk");
        chunk.Property(c => c.Embedding).HasColumnType("vector");
        chunk.HasOne<DocumentJob>().WithMany().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
