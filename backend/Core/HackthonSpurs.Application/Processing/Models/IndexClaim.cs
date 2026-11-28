namespace HackthonSpurs.Application.Processing;

/// <summary>Describe ownership of one index attempt and its persisted OCR source.</summary>
/// <param name="Id">Opaque receipt being indexed.</param>
/// <param name="Token">Current lease ownership token.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="Reading">Stored result; no original bytes.</param>
public sealed record IndexClaim(Guid Id, Guid Token, int Attempt, string Reading);
