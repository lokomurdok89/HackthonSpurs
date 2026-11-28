namespace HackthonSpurs.Application.Processing;

public interface IDocumentReader
{
    /// <summary>Read the expected original and return validated evidence or a classified failure.</summary>
    /// <param name="claim">Lease metadata identifying this attempt.</param>
    /// <param name="stopping">Worker shutdown token.</param>
    Task<ReadOutcome> ReadAsync(ReadingClaim claim, CancellationToken stopping);
}
