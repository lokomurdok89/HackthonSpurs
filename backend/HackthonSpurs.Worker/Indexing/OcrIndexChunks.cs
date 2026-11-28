using HackthonSpurs.Resources;
using System.Text.Json;
using HackthonSpurs.Documents.Embeddings;

namespace HackthonSpurs.Worker.Indexing;

internal sealed record OcrIndexChunk(int Page, string Text);

internal static class OcrIndexChunks
{
    /// <summary>Split a validated reading into bounded excerpts without losing its original page locators.</summary>
    /// <param name="reading">Persisted OCR result JSON; never read original file bytes here.</param>
    /// <param name="id">Claimed document identifier expected in the reading.</param>
    internal static IReadOnlyList<OcrIndexChunk>? Create(string reading, Guid id)
    {
        try
        {
            using var doc = JsonDocument.Parse(reading, new JsonDocumentOptions { MaxDepth = Policy.ReadingDepth });
            var root = doc.RootElement;
            if (root.GetProperty(WireFields.DocumentId).GetGuid() != id || root.GetProperty(WireFields.Version).GetString() != Wire.Version ||
                root.GetProperty(WireFields.ReaderVersion).GetString()?.StartsWith(Wire.SpurPrefix, StringComparison.Ordinal) != true)
                return null;
            List<OcrIndexChunk> chunks = [];
            int previousPage = 0;
            foreach (var fragment in root.GetProperty(WireFields.Fragments).EnumerateArray())
            {
                var locator = fragment.GetProperty(WireFields.Locator);
                if (fragment.GetProperty(WireFields.Method).GetString() != Wire.Ocr ||
                    locator.GetProperty(WireFields.Kind).GetString() != Wire.Page ||
                    !locator.GetProperty(Wire.Page).TryGetInt32(out int page) || page <= previousPage || page > Policy.MaxPages ||
                    fragment.GetProperty(WireFields.Text).GetString() is not { Length: > 0 and <= Policy.MaxTranscriptLength } text ||
                    string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
                    return null;
                previousPage = page;
                Split(text, page, chunks);
            }
            return chunks;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Split one page into bounded Unicode-safe excerpts and omit whitespace-only slices.</summary>
    /// <param name="text">Transcribed text for this page.</param>
    /// <param name="page">Original one-based page number.</param>
    /// <param name="chunks">Destination to append page-ordered excerpts.</param>
    private static void Split(string text, int page, List<OcrIndexChunk> chunks)
    {
        for (int offset = 0; offset < text.Length;)
        {
            int length = Math.Min(text.Length - offset, SpurEmbeddingClient.MaxInputLength);
            if (offset + length < text.Length && char.IsHighSurrogate(text[offset + length - 1])) length--;
            var part = text.AsSpan(offset, length).Trim();
            if (!part.IsEmpty) chunks.Add(new(page, part.ToString()));
            offset += length;
        }
    }
}
