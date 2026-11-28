using HackthonSpurs.Resources;
using System.Globalization;

namespace HackthonSpurs.Worker.Ocr.Rendering;

/// <summary>One locally prepared image retaining its original one-based page number.</summary>
internal sealed record PageBand(int Page, string Path, string Media);

/// <summary>Bound the image size, serialized size and call count before the first billable request.</summary>
/// <param name="target">Inclusive image byte budget.</param>
/// <param name="jsonLimit">Inclusive UTF-8 wire budget.</param>
/// <param name="adaptive">Try an adaptive 1-bit candidate on oversized grayscale pages only.</param>
/// <param name="model">Same public model identifier as the reader; no credentials.</param>
internal sealed class BandPlanner(int target, int jsonLimit, string model, bool adaptive = true)
{
    /// <summary>Test the actual encoded image bytes and the exact provider envelope for media.</summary>
    internal bool Fits(byte[] bytes, string media) =>
        bytes.Length <= target && SpurPayload.Encode(model, bytes, media).Length <= jsonLimit;

    /// <summary>Prepare all paths of media in scratch dir, in reading order, before ct expires.</summary>
    internal async Task<List<PageBand>> PrepareAsync(string[] paths, string media, string dir, CancellationToken ct)
    {
        var plan = new List<PageBand>();
        var total = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            var path = await LosslessPages.FitAsync(paths[i], media, target, dir, ct);
            var stack = new Stack<PageBand>();
            stack.Push(new(i + 1, path, media));
            int count = 1;
            while (stack.TryPop(out var band))
            {
                bool imageFits = new FileInfo(band.Path).Length <= target;
                if (imageFits && Fits(await File.ReadAllBytesAsync(band.Path, ct), band.Media))
                {
                    plan.Add(band);
                    continue;
                }
                if (adaptive && count == 1)
                {
                    // Also validate direct PNGs that fit the image budget but exceeded the JSON budget.
                    if (imageFits && band.Media == Media.Png) LosslessPages.CheckPng(band.Path);
                    var candidate = Path.Combine(dir, Guid.NewGuid().ToString("N"));
                    await DocumentRenderer.RunAsync(RenderSettings.BilevelTool,
                        [band.Path, band.Media, candidate, target.ToString(CultureInfo.InvariantCulture)], dir, ct);
                    if (File.Exists(candidate) && Fits(await File.ReadAllBytesAsync(candidate, ct), Media.Png))
                    {
                        plan.Add(new(band.Page, candidate, Media.Png));
                        continue;
                    }
                }
                var reason = imageFits ? Errors.OcrJsonOverBudget.Code : Errors.OcrPageOverBudget.Code;
                if (++count > Policy.MaxBandsPerPage || ++total > Policy.MaxBandsPerDocument)
                    throw new RenderFailure(reason);
                string upper = Path.Combine(dir, Guid.NewGuid().ToString("N"));
                string lower = Path.Combine(dir, Guid.NewGuid().ToString("N"));
                var result = await DocumentRenderer.RunAsync(RenderSettings.PythonTool,
                    [RenderSettings.BandScript, band.Path, upper, lower, Policy.MaxOptimizePixels.ToString(CultureInfo.InvariantCulture)], dir, ct);
                if (!string.IsNullOrWhiteSpace(result)) throw new RenderFailure(reason);
                stack.Push(new(band.Page, lower, Media.Png));
                stack.Push(new(band.Page, upper, Media.Png));
            }
        }
        return plan;
    }
}
