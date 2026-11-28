namespace HackthonSpurs.Application.Commands;

/// <summary>Expose only bounded staging metadata and operations; disposal removes unpublished staging, never the final original.</summary>
public interface IStagedOriginal : IDisposable
{
    Guid Id { get; }
    string Sha256 { get; }
    long Length { get; }

    /// <summary>Identify the actual format and report any format-specific limit violation.</summary>
    /// <param name="declaredType">Untrusted media type submitted by the caller.</param>
    /// <param name="spur">Whether external OCR formats are enabled for this upload.</param>
    OriginalInspection Inspect(string declaredType, bool spur);

    /// <summary>Move this original into durable storage without exposing its location.</summary>
    void Publish();
}
