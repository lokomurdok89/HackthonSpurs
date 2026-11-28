using HackthonSpurs.Resources;

namespace HackthonSpurs.Reader.Spur.Contracts;

/// <summary>Carry a sanitized HTTP status and machine-readable reader error.</summary>
/// <param name="error">Catalog entry safe to return from the private reader.</param>
internal sealed class ReaderFailure(BackendError error) : Exception(error.Code)
{
    public BackendError Error { get; } = error;
}
