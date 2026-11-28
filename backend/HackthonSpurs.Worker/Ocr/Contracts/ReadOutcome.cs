namespace HackthonSpurs.Worker.Ocr.Contracts;

/// <summary>Carry either validated reading JSON or a classified reader failure.</summary>
/// <param name="Json">Validated public reading on success.</param>
/// <param name="Error">Safe failure code.</param>
/// <param name="Retryable">Whether the lane may schedule another attempt.</param>
internal sealed record ReadOutcome(string? Json = null, string? Error = null, bool Retryable = false);
