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
    public bool SendToOcr { get; set; }
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
