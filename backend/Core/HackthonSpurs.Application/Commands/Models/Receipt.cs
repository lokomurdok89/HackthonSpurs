namespace HackthonSpurs.Application.Commands;

/// <summary>Return an admission decision and, on acceptance, the opaque receipt ID.</summary>
/// <param name="Outcome">Admission or replay decision.</param>
/// <param name="Id">Accepted receipt, if any.</param>
public readonly record struct Receipt(Admission Outcome, Guid Id = default);
