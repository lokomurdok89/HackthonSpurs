using System.Buffers;
using System.Security.Cryptography;

namespace HackthonSpurs.Documents.Storage;

public sealed record StorageSettings(string Root, long MaxFileBytes);
public sealed class UploadTooLargeException : Exception;

// Paths derive exclusively from server-generated IDs, never from uploaded filenames.
public sealed class OriginalStore(StorageSettings settings)
{
    public string GetOriginalPath(Guid id) => Path.Combine(settings.Root, $"{id:N}.original");

    public async Task<StagedFile> StageAsync(Stream source, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.Root);
        var id = Guid.NewGuid();
        var temporary = Path.Combine(settings.Root, $"{id:N}.part");
        var final = Path.Combine(settings.Root, $"{id:N}.original");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        long count = 0;
        try
        {
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            while (true)
            {
                int n;
                try { n = await source.ReadAsync(buffer.AsMemory(), ct); }
                catch (IOException ex) when (ex.GetType() == typeof(IOException))
                { throw new InvalidDataException("Incomplete upload stream.", ex); }
                if (n == 0) break;
                count += n;
                if (count > settings.MaxFileBytes) throw new UploadTooLargeException();
                hash.AppendData(buffer, 0, n);
                await output.WriteAsync(buffer.AsMemory(0, n), ct);
            }
            output.Flush(flushToDisk: true);
            return new(id, temporary, final, count, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}

public sealed record StagedFile(Guid Id, string TemporaryPath, string FinalPath, long Length, string Sha256) : IDisposable
{
    public void Publish() => File.Move(TemporaryPath, FinalPath);
    // Never delete a published original: a lost commit acknowledgement is ambiguous.
    public void Dispose() => File.Delete(TemporaryPath);
}
