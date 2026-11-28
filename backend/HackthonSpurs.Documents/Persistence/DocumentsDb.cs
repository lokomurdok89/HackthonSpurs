using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace HackthonSpurs.Documents.Persistence;

// A single job per uploaded document: inserting one row queues it atomically.
// The Worker updates the same row for terminal state and result publication.
public enum JobState { Queued, Processing, Completed, Failed }

public sealed class DocumentJob
{
    public Guid Id { get; set; }
    public Guid OperationKey { get; set; }
    public required string Sha256 { get; set; }
    public required string MediaType { get; set; }
    public long ByteCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public JobState State { get; set; }
    public int Attempt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? FailureCode { get; set; }
    public string? ResultJson { get; set; }
}

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string? SourceName { get; set; }
    public required string Locator { get; set; }
    public required string Text { get; set; }
    public required string Model { get; set; }
    public required Vector Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DocumentsDb(DbContextOptions<DocumentsDb> options) : DbContext(options)
{
    public DbSet<DocumentJob> Documents => Set<DocumentJob>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasPostgresExtension("vector");

        var job = model.Entity<DocumentJob>();
        job.ToTable("document_jobs", t =>
        {
            t.HasCheckConstraint("ck_attempt", "\"Attempt\" >= 0");
            t.HasCheckConstraint("ck_state", "\"State\" IN ('Queued','Processing','Completed','Failed')");
            t.HasCheckConstraint("ck_result", "(\"State\" = 'Completed') = (\"ResultJson\" IS NOT NULL)");
            t.HasCheckConstraint("ck_failure", "(\"State\" = 'Failed') = (\"FailureCode\" IS NOT NULL)");
        });
        job.HasKey(d => d.Id);
        job.Property(d => d.Id).ValueGeneratedNever();
        job.HasIndex(d => d.OperationKey).IsUnique().HasDatabaseName("ux_document_operation");
        job.Property(d => d.Sha256).HasMaxLength(64);
        job.Property(d => d.State).HasConversion<string>();
        job.Property(d => d.ResultJson).HasColumnType("jsonb");

        var chunk = model.Entity<DocumentChunk>();
        chunk.ToTable("document_chunks", t =>
        {
            t.HasCheckConstraint("ck_chunk_index", "\"ChunkIndex\" >= 0");
        });
        chunk.HasKey(c => c.Id);
        chunk.Property(c => c.Id).ValueGeneratedNever();
        chunk.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique().HasDatabaseName("ux_document_chunk");
        chunk.Property(c => c.Embedding).HasColumnType("vector");
        chunk.HasOne<DocumentJob>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
