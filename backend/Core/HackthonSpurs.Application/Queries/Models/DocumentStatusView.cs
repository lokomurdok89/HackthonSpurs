namespace HackthonSpurs.Application.Queries;

/// <summary>Carry progress and failure details without loading the reading.</summary>
/// <param name="State">Public name of the processing state.</param>
/// <param name="Attempt">Number of claims made.</param>
/// <param name="FailureCode">Safe terminal error code, if any.</param>
/// <param name="Failed">Whether to show the failure field in the public response.</param>
public sealed record DocumentStatusView(string State, int Attempt, string? FailureCode, bool Failed);
