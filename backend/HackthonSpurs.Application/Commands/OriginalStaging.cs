namespace HackthonSpurs.Application.Commands;

public interface IOriginalStaging
{
    /// <summary>Stage and hash one bounded upload, without accepting it as a document.</summary>
    /// <param name="source">Untrusted original byte stream.</param>
    /// <param name="ct">Request cancellation.</param>
    Task<IStagedOriginal> StageAsync(Stream source, CancellationToken ct);
}

public sealed class UploadTooLargeException : Exception;
