namespace HackthonSpurs.Application.Processing;

/// <summary>Carry the decided reading publication, terminal failure, or retry.</summary>
/// <param name="Json">Validated reading on success.</param>
/// <param name="Error">Safe terminal error code; omitted for a retry.</param>
/// <param name="RetryDelay">Delay before the next claim, if permitted.</param>
public sealed record ReadingCompletion(string? Json, string? Error, TimeSpan? RetryDelay);
