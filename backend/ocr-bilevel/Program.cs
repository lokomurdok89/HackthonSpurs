using System.Globalization;
using HackthonSpurs.Resources;
using ImageMagick;

// Private, killable Worker subprocess. No network calls, credentials, original-store reads or PDF delegates.
if (args.Length != 4 || args[1] is not (Media.Png or Media.Jpeg) ||
    !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var budget) ||
    budget is < 1 or > Policy.DefaultImageTarget) return 1;
try
{
    BilevelPage.TryWrite(args[0], args[1], args[2], budget);
    return 0;
}
catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
{
    // The parent converts the exit status to a safe error without forwarding file contents.
    return 1;
}

internal static class BilevelPage
{
    /// <summary>Write a bounded derivative to output only when the raster at source, of known media, is eligible and fits budget.</summary>
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
