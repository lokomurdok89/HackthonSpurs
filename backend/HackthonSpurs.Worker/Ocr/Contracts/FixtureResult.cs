using HackthonSpurs.Resources;
using System.Text.Json;

namespace HackthonSpurs.Worker.Ocr.Contracts;

// Intentionally narrower than the public ReadResult. Do not publish any other text,
// locators or extension properties from the synthetic adapter as genuine evidence.
internal static class FixtureResult
{
    /// <summary>Reject mismatched identity or evidence and reconstruct the allowed public reading.</summary>
    /// <param name="body">Bounded reader response bytes.</param>
    /// <param name="id">Requested document UUID.</param>
    public static ReadOutcome Validate(byte[] body, Guid id)
    {
        try { return Parse(body, id); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        { return new(Error: Errors.InvalidReaderResult.Code); }
    }

    /// <summary>Extract only the known fixture fragment from the reader response.</summary>
    /// <param name="body">Bounded reader response bytes.</param>
    /// <param name="id">Requested document UUID.</param>
    private static ReadOutcome Parse(byte[] body, Guid id)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = Policy.ReadingDepth });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, WireFields.DocumentId) != id.ToString("D") ||
            Text(root, WireFields.Version) != Wire.Version || Text(root, WireFields.ReaderVersion) != Wire.FixtureVersion ||
            !Array(root, WireFields.Warnings, 0, out _) || !Array(root, WireFields.Fragments, 1, out var fragments))
            return new(Error: Errors.InvalidReaderResult.Code);

        var f = fragments[0];
        if (Text(f, WireFields.Method) != Wire.Native || Text(f, WireFields.Text) != Wire.FixtureText ||
            !f.TryGetProperty(WireFields.Locator, out var loc) || Text(loc, WireFields.Kind) != Wire.CsvCell ||
            !Position(loc, WireFields.Row, Wire.FixtureRow) || !Position(loc, WireFields.Column, Wire.FixtureColumn)) return new(Error: Errors.InvalidReaderResult.Code);
        // Reconstruct the public subset rather than passing through untrusted JSON.
        return new(Json: JsonSerializer.Serialize(new
        {
            documentId = id,
            version = Text(root, WireFields.Version),
            readerVersion = Text(root, WireFields.ReaderVersion),
            fragments = new[] { new { method = Text(f, WireFields.Method), text = Text(f, WireFields.Text),
                locator = new { kind = Wire.CsvCell, row = Wire.FixtureRow, column = Wire.FixtureColumn } } },
            warnings = System.Array.Empty<object>()
        }));
    }

    /// <summary>Read a string member without coercing other JSON values.</summary>
    /// <param name="e">JSON object containing the member.</param>
    /// <param name="key">JSON member name.</param>
    private static string? Text(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() : null;
    /// <summary>Check that a locator coordinate matches the expected integer.</summary>
    /// <param name="e">JSON object containing the member.</param>
    /// <param name="key">JSON member name.</param>
    /// <param name="expected">Required coordinate.</param>
    private static bool Position(JsonElement e, string key, int expected) =>
        e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) && n == expected;
    /// <summary>Locate a JSON array and verify its exact size.</summary>
    /// <param name="e">JSON object containing the member.</param>
    /// <param name="key">JSON member name.</param>
    /// <param name="length">Required item count.</param>
    /// <param name="items">Receives the matched array.</param>
    private static bool Array(JsonElement e, string key, int length, out JsonElement items) =>
        e.TryGetProperty(key, out items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == length;
}
