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
