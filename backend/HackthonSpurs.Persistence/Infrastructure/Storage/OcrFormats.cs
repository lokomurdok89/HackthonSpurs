using HackthonSpurs.Resources;

namespace HackthonSpurs.Documents.Storage;

public static class OcrFormats
{
    public const string Word = Media.Word;
    public const string Slides = Media.Slides;
    public const int MaxImageBytes = Policy.MaxImageBytes;
    public const int MaxDocumentBytes = Policy.MaxDocumentBytes;
    public const int MaxPages = Policy.MaxPages;

    /// <summary>Choose an internal converter suffix, never a client-supplied path.</summary>
    /// <param name="media">Verified media type persisted at acceptance.</param>
    public static string? Extension(string media) => media switch
    {
        Media.Pdf => RenderSettings.PdfSuffix,
        Word => RenderSettings.WordSuffix,
        Slides => RenderSettings.SlidesSuffix,
        Media.Workbook => RenderSettings.WorkbookSuffix,
        Media.Csv or Media.Plain => RenderSettings.TextSuffix,
        _ => null
    };
}
