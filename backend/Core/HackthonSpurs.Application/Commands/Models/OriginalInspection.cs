namespace HackthonSpurs.Application.Commands;

public readonly record struct OriginalInspection(string? MediaType, bool TooLarge = false);
