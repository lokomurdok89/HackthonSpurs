using HackthonSpurs.Documents.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Documents.Persistence;

/// <summary>Configure persistence for document jobs and their reading results.</summary>
/// <param name="options">EF Core provider and connection options.</param>
public sealed class DocumentsDb(DbContextOptions<DocumentsDb> options) : DbContext(options)
{
    public DbSet<DocumentJob> Documents => Set<DocumentJob>();
    public DbSet<DocumentIndexJob> IndexJobs => Set<DocumentIndexJob>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<WorkspaceUser> WorkspaceUsers => Set<WorkspaceUser>();
    public DbSet<WorkspaceSession> WorkspaceSessions => Set<WorkspaceSession>();

    /// <summary>Configure job mappings, uniqueness and state constraints.</summary>
    /// <param name="model">EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder model)
    {
        DocumentMappings.Configure(model);
        IndexMappings.Configure(model);
        WorkspaceMappings.Configure(model);
    }
}
