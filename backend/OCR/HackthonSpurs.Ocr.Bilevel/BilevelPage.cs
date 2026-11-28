using HackthonSpurs.Resources;
using ImageMagick;

namespace HackthonSpurs.Ocr.Bilevel;

internal static class BilevelPage
{
    /// <summary>Write a bounded derivative only when the source raster is eligible and fits the byte budget.</summary>
    /// <param name="source">Prepared source raster path in the worker's scratch directory.</param>
    /// <param name="media">Verified PNG or JPEG media type.</param>
    /// <param name="output">New derivative path in the worker's scratch directory.</param>
    /// <param name="budget">Maximum encoded derivative size in bytes.</param>
    internal static void TryWrite(string source, string media, string output, int budget)
    {
        ResourceLimits.Thread = Policy.BilevelThreads;
        var settings = new MagickReadSettings { Format = media == Media.Png ? MagickFormat.Png : MagickFormat.Jpeg, FrameCount = 1 };
        using var input = File.OpenRead(source);
        var info = new MagickImageInfo(input, settings);
        if ((ulong)info.Width * info.Height is 0 or > Policy.MaxOptimizePixels) return;
        input.Position = 0;
        using var image = new MagickImage(input, settings);
        if (image.HasAlpha && !image.IsOpaque) return;
        if (image.DetermineColorType() is not (ColorType.Grayscale or ColorType.GrayscaleAlpha or ColorType.Bilevel)) return;
        image.AutoOrient();
        image.ColorSpace = ColorSpace.Gray;
        image.AdaptiveThreshold(Policy.BilevelWindow, Policy.BilevelWindow, Policy.BilevelOffset);
        if (image.TotalColors < 2) return;
        image.Strip();
        // Orientation has already been applied; do not emit an orNT ancillary chunk.
        image.Orientation = Orientation.Undefined;
        image.Settings.SetDefine(MagickFormat.Png, RenderSettings.BilevelBitDepth, RenderSettings.MonoBitValue);
        image.Settings.SetDefine(MagickFormat.Png, RenderSettings.BilevelColorType, RenderSettings.MonoGrayValue);
        image.Settings.SetDefine(MagickFormat.Png, RenderSettings.BilevelExclude, RenderSettings.AllMarkers);
        image.Settings.SetDefine(MagickFormat.Png, RenderSettings.BilevelCompression, RenderSettings.MonoCompressionValue);
        using var encoded = new MemoryStream();
        image.Write(encoded, MagickFormat.Png);
        if (encoded.Length > budget) return;
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoded.Position = 0;
        encoded.CopyTo(file);
    }
}
