using System.Buffers.Binary;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Worker;

/// <summary>Prepare page files exclusively inside the per-job scratch directory, never in the originals store.</summary>
internal static class LosslessPages
{
    private static readonly byte[] Header = System.Text.Encoding.ASCII.GetBytes(RenderSettings.PngHeaderChunk);
    private static readonly byte[] Animation = System.Text.Encoding.ASCII.GetBytes(RenderSettings.PngAnimationChunk);
    private static readonly byte[] ImageData = System.Text.Encoding.ASCII.GetBytes(RenderSettings.PngDataChunk);

    /// <summary>Write verified image bytes of type media to a private copy in dir, observing ct.</summary>
    internal static async Task<string> StageAsync(byte[] bytes, string media, string dir, CancellationToken ct)
    {
        var path = Path.Combine(dir, media == Media.Png ? RenderSettings.ScratchPng : RenderSettings.ScratchJpeg);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    /// <summary>Return path unchanged if it fits target; otherwise losslessly optimize the specified media in dir within ct.</summary>
    internal static async Task<string> FitAsync(string path, string media, int target, string dir, CancellationToken ct)
    {
        if (new FileInfo(path).Length <= target) return path;
        if (media == Media.Png)
        {
            CheckPng(path);
            await DocumentRenderer.RunAsync(RenderSettings.PngTool, [RenderSettings.PngEffort, path], dir, ct);
        }
        else
        {
            var output = Path.Combine(dir, RenderSettings.OptimizedJpeg);
            await DocumentRenderer.RunAsync(RenderSettings.JpegTool,
                [RenderSettings.JpegStrict, RenderSettings.JpegCopy, RenderSettings.AllMarkers, RenderSettings.JpegOptimize,
                 RenderSettings.JpegMemory, RenderSettings.MemoryCap, RenderSettings.JpegScanLimit, RenderSettings.ScanCap,
                 RenderSettings.JpegOut, output, path], dir, ct);
            path = output;
        }
        // An oversized optimized copy is retained for the band planner.
        return path;
    }

    /// <summary>Bound decoded pixels and refuse animated PNGs before optimizing the scratch file at path.</summary>
    internal static void CheckPng(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[33];
        file.ReadExactly(header);
        var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
        if (!header.Slice(12, 4).SequenceEqual(Header) || (ulong)width * height is 0 or > Policy.MaxOptimizePixels)
            throw new RenderFailure(Errors.DocumentNotRenderable.Code);
        Span<byte> chunk = stackalloc byte[8];
        while (file.Position < file.Length)
        {
            file.ReadExactly(chunk);
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunk);
            if (chunk[4..].SequenceEqual(Animation) || length + 4L > file.Length - file.Position)
                throw new RenderFailure(Errors.DocumentNotRenderable.Code);
            if (chunk[4..].SequenceEqual(ImageData)) return;
            file.Seek(length + 4L, SeekOrigin.Current);
        }
        throw new RenderFailure(Errors.DocumentNotRenderable.Code);
    }
}
