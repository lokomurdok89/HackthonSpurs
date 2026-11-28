using HackthonSpurs.Resources;
using System.Text.Json;

namespace HackthonSpurs.Worker;

internal static class SpurResult
{
    /// <summary>Reject mismatched identity or evidence and reconstruct the allowed public reading.</summary>
    /// <param name="body">Bounded reader response bytes.</param>
    /// <param name="id">Requested document UUID.</param>
    /// <param name="page">Expected page in the submitted document.</param>
    internal static ReadOutcome Validate(byte[] body, Guid id, int page = 1)
    {
        try
        {
            using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = Policy.ReadingDepth });
            var r = doc.RootElement;
            if (r.GetProperty(WireFields.DocumentId).GetString() != id.ToString("D") || r.GetProperty(WireFields.Version).GetString() != Wire.Version)
                return Invalid();
            var reader = r.GetProperty(WireFields.ReaderVersion).GetString();
            const string prefix = Wire.SpurPrefix;
            if (reader is null || !reader.StartsWith(prefix, StringComparison.Ordinal) ||
                reader.Length <= prefix.Length || reader.Length > prefix.Length + Policy.MaxModelLength ||
                !reader[prefix.Length..].All(c => char.IsAsciiLetterOrDigit(c) || ServiceDefaults.ModelPunctuation.Contains(c))) return Invalid();
            var f = r.GetProperty(WireFields.Fragments);
            var w = r.GetProperty(WireFields.Warnings);
            var locator = new { kind = Wire.Page, page };
            object[] fragments = [], warnings = [];
            if (f.GetArrayLength() == 1 && w.GetArrayLength() == 0 && MatchesPage(f[0], page) &&
                f[0].GetProperty(WireFields.Method).GetString() == Wire.Ocr)
            {
                var text = f[0].GetProperty(WireFields.Text).GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > Policy.MaxTranscriptLength || text.Contains('\0')) return Invalid();
                fragments = [new { method = Wire.Ocr, locator, text }];
            }
            else if (f.GetArrayLength() == 0 && w.GetArrayLength() == 1 && MatchesPage(w[0], page) &&
                w[0].GetProperty(Wire.Code).GetString() == Errors.NoReadableText.Code)
                warnings = [new { code = Errors.NoReadableText.Code, locator, message = Errors.NoReadableText.Message }];
            else return Invalid();
            return new(Json: JsonSerializer.Serialize(new { documentId = id, version = Wire.Version, readerVersion = reader, fragments, warnings }));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { return Invalid(); }
    }

    /// <summary>Publish a complete ordered reading; refuse mixed reader versions.</summary>
    /// <param name="results">Locally validated page readings, in page order.</param>
    /// <param name="id">Document identifier common to every page.</param>
    internal static ReadOutcome Combine(IEnumerable<string> results, Guid id)
    {
        List<JsonElement> fragments = [], warnings = [];
        string? reader = null;
        foreach (string result in results)
        {
            using var page = JsonDocument.Parse(result);
            var root = page.RootElement;
            var version = root.GetProperty(WireFields.ReaderVersion).GetString();
            reader ??= version;
            if (reader != version) return Invalid();
            fragments.AddRange(root.GetProperty(WireFields.Fragments).EnumerateArray().Select(f => f.Clone()));
            warnings.AddRange(root.GetProperty(WireFields.Warnings).EnumerateArray().Select(w => w.Clone()));
        }
        return new(Json: JsonSerializer.Serialize(new { documentId = id, version = Wire.Version, readerVersion = reader, fragments, warnings }));
    }

    /// <summary>Verify that the fragment or warning refers to the submitted image page.</summary>
    /// <param name="item">Fragment or warning carrying a locator.</param>
    /// <param name="page">Expected one-based page.</param>
    private static bool MatchesPage(JsonElement item, int page)
    {
        var loc = item.GetProperty(WireFields.Locator);
        return loc.GetProperty(WireFields.Kind).GetString() == Wire.Page &&
            loc.GetProperty(Wire.Page).TryGetInt32(out var n) && n == page;
    }
    /// <summary>Reject a malformed page result without publishing any content.</summary>
    private static ReadOutcome Invalid() => new(Error: Errors.InvalidReaderResult.Code);
}
