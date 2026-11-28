using System.Text;
using HackthonSpurs.Worker;
using HackthonSpurs.Worker.Tests;

var id = Guid.NewGuid();
var input = $$$"""
    {"documentId":"{{{id}}}","version":"1","readerVersion":"synthetic-fixture/1","extra":"do not publish",
     "fragments":[{"text":"DEMO-ONLY-014","method":"native","locator":{"kind":"csvCell","row":2,"column":1}}],
     "warnings":[]}
    """;
var cases = new Dictionary<string, string>
{
    ["malformed"] = "{",
    ["wrong document"] = input.Replace(id.ToString(), Guid.NewGuid().ToString()),
    ["wrong version"] = input.Replace("\"version\":\"1\"", "\"version\":\"2\""),
    ["wrong text"] = input.Replace("DEMO-ONLY-014", "unverified text"),
    ["wrong locator"] = input.Replace("\"row\":2", "\"row\":0"),
    ["invalid fragment"] = input.Replace("\"native\"", "null"),
    ["bad unicode"] = input.Replace("DEMO-ONLY-014", "\\ud800"),
    ["missing warnings"] = input.Replace("\"warnings\":[]", "\"warnings\":null")
};
foreach (var (name, json) in cases)
{
    var bad = FixtureResult.Validate(Encoding.UTF8.GetBytes(json), id);
    if (bad.Error != "invalid_reader_result" || bad.Json != null || bad.Retryable)
        throw new InvalidOperationException($"Did not reject: {name}");
}
var good = FixtureResult.Validate(Encoding.UTF8.GetBytes(input), id);
if (good.Json == null || good.Json.Contains("do not publish")) throw new InvalidOperationException("Unsafe result");
Console.WriteLine($"PASS: bounded fixture contract, {cases.Count} invalid cases rejected, extra properties excluded.");


// The paid reader may publish only its own page's text (or one located empty warning).
var pageResult = new
{
    documentId = id,
    version = "1",
    readerVersion = "spur/1:offline",
    fragments = new[] { new { method = "ocr", text = "Synthetic\npart", locator = new { kind = "page", page = 1 } } },
    warnings = Array.Empty<object>(),
    privateExtra = "never publish"
};
var pageJson = System.Text.Json.JsonSerializer.Serialize(pageResult);
var pageBytes = Encoding.UTF8.GetBytes(pageJson);
var pageGood = SpurResult.Validate(pageBytes, id);
if (pageGood.Json == null || pageGood.Json.Contains("never publish") || SpurResult.Validate(pageBytes, Guid.NewGuid()).Json != null)
    throw new InvalidOperationException("SPUR result isolation failed.");
foreach (var bad in new[] { "{}", pageJson.Replace("\"page\":1", "\"page\":2"), pageJson.Replace("spur/1:", "fixture/1:"),
    pageJson.Replace("\"ocr\"", "\"native\""), pageJson.Replace("Synthetic", "\\u0000") })
    if (SpurResult.Validate(Encoding.UTF8.GetBytes(bad), id).Error != "invalid_reader_result")
        throw new InvalidOperationException("Invalid SPUR result accepted.");
var empty = new
{
    documentId = id,
    version = "1",
    readerVersion = "spur/1:offline",
    fragments = Array.Empty<object>(),
    warnings = new[] { new { code = "no_readable_text", locator = new { kind = "page", page = 1 } } }
};
if (SpurResult.Validate(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(empty), id).Json == null)
    throw new InvalidOperationException("Empty reading lost its warning.");
Console.WriteLine("PASS: matching SPUR page, plain text, bounded projection and located empty warning.");
await EmbeddingReview.RunAsync();
await ReaderProblemChecks.RunAsync();
