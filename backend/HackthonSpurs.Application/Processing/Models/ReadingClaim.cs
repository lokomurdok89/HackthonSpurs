namespace HackthonSpurs.Application.Processing;

/// <summary>Describe lease ownership and the expected original for one reading attempt.</summary>
/// <param name="Id">Opaque document receipt.</param>
/// <param name="Token">Current lease ownership token.</param>
/// <param name="Attempt">Number of claims made, including this one.</param>
/// <param name="ByteCount">Expected original length.</param>
/// <param name="MediaType">Verified media type.</param>
/// <param name="Hash">Expected SHA-256 of the original.</param>
/// <param name="SendToOcr">Whether this is the explicit external OCR lane.</param>
public sealed record ReadingClaim(Guid Id, Guid Token, int Attempt, long ByteCount, string MediaType, string Hash, bool SendToOcr);
