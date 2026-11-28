using System.Text.Json;

namespace HackthonSpurs.Worker;

// Intentionally narrower than the public ReadResult. Do not publish any other text,
// locators or extension properties from the synthetic adapter as genuine evidence.
internal static class FixtureResult
{
    public static ReadOutcome Validate(byte[] body, Guid id)
    {
        try { return Parse(body, id); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        { return new(Error: "invalid_reader_result"); }
    }

    private static ReadOutcome Parse(byte[] body, Guid id)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "documentId") != id.ToString("D") ||
            Text(root, "version") != "1" || Text(root, "readerVersion") != "synthetic-fixture/1" ||
            !Array(root, "warnings", 0, out _) || !Array(root, "fragments", 1, out var fragments))
            return new(Error: "invalid_reader_result");

        var f = fragments[0];
        if (Text(f, "method") != "native" || Text(f, "text") != "DEMO-ONLY-014" ||
            !f.TryGetProperty("locator", out var loc) || Text(loc, "kind") != "csvCell" ||
            !Position(loc, "row", 2) || !Position(loc, "column", 1)) return new(Error: "invalid_reader_result");
        // Reconstruct the public subset rather than passing through untrusted JSON.
        return new(Json: JsonSerializer.Serialize(new {
            documentId = id, version = Text(root, "version"), readerVersion = Text(root, "readerVersion"),
            fragments = new[] { new { method = Text(f, "method"), text = Text(f, "text"),
                locator = new { kind = "csvCell", row = 2, column = 1 } } }, warnings = System.Array.Empty<object>()
        }));
    }

    private static string? Text(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() : null;
    private static bool Position(JsonElement e, string key, int expected) =>
        e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) && n == expected;
    private static bool Array(JsonElement e, string key, int length, out JsonElement items) =>
        e.TryGetProperty(key, out items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == length;
}
