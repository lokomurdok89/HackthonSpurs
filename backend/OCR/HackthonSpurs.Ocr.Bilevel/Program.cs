using System.Globalization;
using HackthonSpurs.Ocr.Bilevel;
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
