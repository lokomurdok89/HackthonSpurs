namespace HackthonSpurs.Reader.Spur.Contracts;

internal interface IPageTranscriber
{
    string ReaderVersion { get; }
    /// <summary>Return the visible text for one image or raise a classified reader failure.</summary>
    /// <param name="image">Bytes of one validated page image.</param>
    /// <param name="mediaType">PNG or JPEG media type.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    Task<string> TranscribeAsync(byte[] image, string mediaType, CancellationToken ct);
}
