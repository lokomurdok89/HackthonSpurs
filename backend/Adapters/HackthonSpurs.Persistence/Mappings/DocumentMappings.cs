using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence.Mappings;

internal static class DocumentMappings
{
    /// <summary>Map document jobs, including operation-key uniqueness and state constraints.</summary>
    /// <param name="model">EF Core model builder for the document database.</param>
    internal static void Configure(ModelBuilder model)
    {
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
        job.HasIndex(d => d.OperationKey).IsUnique().HasDatabaseName(Wire.OperationIndex);
        job.Property(d => d.Sha256).HasMaxLength(64);
        job.Property(d => d.State).HasConversion<string>();
        job.Property(d => d.ResultJson).HasColumnType("jsonb");
    }
}
