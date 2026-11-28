namespace HackthonSpurs.Application.Processing;

/// <summary>Provide a safe operational summary of one processed lease.</summary>
/// <param name="Id">Opaque receipt.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="Outcome">Stable operational label or error code.</param>
public sealed record JobAttemptReport(Guid Id, int Attempt, string Outcome);
