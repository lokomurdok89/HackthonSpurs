using HackthonSpurs.Application.Commands;
using HackthonSpurs.Resources;
using System.Buffers;
using System.Security.Cryptography;

namespace HackthonSpurs.Documents.Storage;

/// <summary>Define the private storage boundary and upload size limit.</summary>
/// <param name="Root">Absolute private originals directory.</param>
/// <param name="MaxFileBytes">Maximum bytes accepted per file.</param>
public sealed record StorageSettings(string Root, long MaxFileBytes);

// Paths derive exclusively from server-generated IDs, never from uploaded filenames.
/// <summary>Stage bounded uploads in private storage under opaque names.</summary>
/// <param name="settings">Validated process settings.</param>
public sealed class OriginalStore(StorageSettings settings) : IOriginalStaging
{
    /// <summary>Stream an upload into a temporary file while counting and hashing its bytes.</summary>
    /// <param name="source">Untrusted upload stream.</param>
    /// <param name="ct">Cancellation for the current operation.</param>
    public async Task<IStagedOriginal> StageAsync(Stream source, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.Root);
        var id = Guid.NewGuid();
        var temporary = Path.Combine(settings.Root, id.ToString("N") + Wire.StagedSuffix);
        var final = Path.Combine(settings.Root, id.ToString("N") + Wire.OriginalSuffix);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(Policy.StreamBuffer);
        long count = 0;
        try
        {
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, Policy.StreamBuffer, true);
            while (true)
            {
                int n;
                try { n = await source.ReadAsync(buffer.AsMemory(), ct); }
                catch (IOException ex) when (ex.GetType() == typeof(IOException))
                { throw new InvalidDataException(ResourceText.OriginalIncomplete, ex); }
                if (n == 0) break;
                count += n;
                if (count > settings.MaxFileBytes) throw new UploadTooLargeException();
                hash.AppendData(buffer, 0, n);
                await output.WriteAsync(buffer.AsMemory(0, n), ct);
            }
            output.Flush(flushToDisk: true);
            return new StagedFile(id, temporary, final, count, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}

/// <summary>Track the original and temporary paths until admission completes.</summary>
/// <param name="Id">Opaque document identifier.</param>
/// <param name="TemporaryPath">Private staging path.</param>
/// <param name="FinalPath">Destination assigned to the accepted original.</param>
/// <param name="Length">Staged file size in bytes.</param>
/// <param name="Sha256">Digest of the staged bytes.</param>
internal sealed record StagedFile(Guid Id, string TemporaryPath, string FinalPath, long Length, string Sha256) : IStagedOriginal
{
    /// <inheritdoc />
    public OriginalInspection Inspect(string declaredType, bool spur)
    {
        var type = FileIdentification.Identify(TemporaryPath, declaredType, spur);
        return new(type, (type is Media.Png or Media.Jpeg) && Length > OcrFormats.MaxImageBytes);
    }

    /// <summary>Move the complete staged file into its final private location.</summary>
    public void Publish() => File.Move(TemporaryPath, FinalPath);
    // Never delete a published original: a lost commit acknowledgement is ambiguous.
    /// <summary>Remove only the temporary file, never a published original.</summary>
    public void Dispose() => File.Delete(TemporaryPath);
}
