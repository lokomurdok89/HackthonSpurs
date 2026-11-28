namespace HackthonSpurs.Resources;

/// <summary>Describe one stable machine code without storing provider payloads or exception details.</summary>
/// <param name="Code">Public wire identifier or persisted job/warning code.</param>
/// <param name="Message">Safe English description.</param>
/// <param name="HttpStatus">Status at the boundary that produces the problem; null for job-only codes.</param>
/// <param name="Reader">Whether the private page reader may return this code at that exact HTTP status.</param>
public sealed record BackendError(string Code, string Message, int? HttpStatus, bool Reader);
