using HackthonSpurs.Resources;
using System.Diagnostics;
using System.Globalization;
using HackthonSpurs.Documents.Storage;

namespace HackthonSpurs.Worker.Ocr.Rendering;

/// <summary>Classify conversion failures without exposing file contents.</summary>
/// <param name="code">Public failure identifier.</param>
internal sealed class RenderFailure(string code) : Exception(code);

internal static class DocumentRenderer
{
    /// <summary>Lay out the original and rasterize all pages before the first billable request.</summary>
    /// <param name="bytes">Hash-verified original bytes.</param>
    /// <param name="media">Verified original MIME type.</param>
    /// <param name="dir">Private per-job scratch directory owned by the caller.</param>
    /// <param name="ct">Overall job deadline or host shutdown.</param>
    internal static async Task<string[]> RenderAsync(byte[] bytes, string media, string dir, CancellationToken ct)
    {
        var ext = OcrFormats.Extension(media) ?? throw new RenderFailure(Errors.UnsupportedDocument.Code);
        var source = Path.Combine(dir, RenderSettings.SourceStem + ext);
        await File.WriteAllBytesAsync(source, bytes, ct);
        var pdf = Path.Combine(dir, RenderSettings.SourceStem + RenderSettings.PdfSuffix);
        if (ext != RenderSettings.PdfSuffix)
        {
            // For TXT/CSV print the literal text, not executable spreadsheet formulas.
            var profile = Path.Combine(dir, RenderSettings.ProfileDir, RenderSettings.UserDir);
            Directory.CreateDirectory(profile);
            await File.WriteAllTextAsync(Path.Combine(profile, RenderSettings.RegistryFile), RenderSettings.OfficeSecurityProfile, ct);
            List<string> args = [RenderSettings.OfficeProfileArgument + new Uri(Path.Combine(dir, RenderSettings.ProfileDir)).AbsoluteUri,
                RenderSettings.Headless, RenderSettings.NoLogo, RenderSettings.NoDefaults, RenderSettings.NoRestore];
            if (ext == RenderSettings.TextSuffix) args.Add(RenderSettings.TextFilter);
            args.AddRange([RenderSettings.ConvertTo, RenderSettings.PdfFormat, RenderSettings.OutDir, dir, source]);
            await RunAsync(RenderSettings.OfficeTool, args, dir, ct);
        }
        if (!File.Exists(pdf)) throw new RenderFailure(Errors.DocumentNotRenderable.Code);
        var info = await RunAsync(RenderSettings.PdfInfoTool, [pdf], dir, ct);
        var pages = info.Split('\n').FirstOrDefault(l => l.StartsWith(RenderSettings.PageCountPrefix, StringComparison.Ordinal));
        if (pages == null || !int.TryParse(pages[RenderSettings.PageCountPrefix.Length..].Trim(), CultureInfo.InvariantCulture, out int count) || count < 1)
            throw new RenderFailure(Errors.DocumentNotRenderable.Code);
        if (count > OcrFormats.MaxPages) throw new RenderFailure(Errors.TooManyPages.Code);
        var prefix = Path.Combine(dir, RenderSettings.PageStem);
        await RunAsync(RenderSettings.RasterTool, [RenderSettings.PngFlag, RenderSettings.ScaleFlag, RenderSettings.RasterScale, pdf, prefix], dir, ct);
        var paths = Directory.GetFiles(dir, RenderSettings.RasterFiles)
            .OrderBy(path => int.Parse(Path.GetFileNameWithoutExtension(path)[(RenderSettings.PageStem.Length + 1)..], CultureInfo.InvariantCulture)).ToArray();
        if (paths.Length != count || paths.Any(path => new FileInfo(path).Length <= 0))
            throw new RenderFailure(Errors.DocumentNotRenderable.Code);
        return paths;
    }

    /// <summary>Run a fixed converter without a shell, secrets or a shared user profile.</summary>
    /// <param name="tool">Absolute executable supplied by the image.</param>
    /// <param name="args">Arguments constructed by the renderer.</param>
    /// <param name="dir">Job-local home, temp and working directory.</param>
    /// <param name="ct">Cancellation that terminates the process tree.</param>
    internal static async Task<string> RunAsync(string tool, List<string> args, string dir, CancellationToken ct)
    {
        var start = new ProcessStartInfo(tool)
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment.Clear();
        start.Environment[RenderSettings.PathVariable] = RenderSettings.SearchPath;
        start.Environment[RenderSettings.HomeVariable] = start.Environment[RenderSettings.TempVariable] = dir;
        start.Environment[RenderSettings.LocaleVariable] = RenderSettings.Locale;
        start.Environment[RenderSettings.ConcurrencyVariable] = RenderSettings.Concurrency;
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new RenderFailure(Errors.RendererUnavailable.Code);
        using var onCancel = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* Process exited before cancellation. */ }
        });
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
            await Task.WhenAll(process.WaitForExitAsync(ct), stdout, stderr);
            if (process.ExitCode != 0) throw new RenderFailure(Errors.DocumentNotRenderable.Code);
            return await stdout;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }
}
