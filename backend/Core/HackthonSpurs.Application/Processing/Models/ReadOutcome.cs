namespace HackthonSpurs.Application.Processing;

/// <summary>Carry the validated reading or a classified failure from the reader adapter.</summary>
/// <param name="Json">Validated public reading on success.</param>
/// <param name="Error">Safe reader failure code.</param>
/// <param name="Retryable">Whether the reported failure admits another attempt.</param>
public sealed record ReadOutcome(string? Json = null, string? Error = null, bool Retryable = false);
