using System.Text;
using HackthonSpurs.Worker;

var id = Guid.NewGuid();
var input = $$$"""
    {"documentId":"{{{id}}}","version":"1","readerVersion":"synthetic-fixture/1","extra":"do not publish",
     "fragments":[{"text":"DEMO-ONLY-014","method":"native","locator":{"kind":"csvCell","row":2,"column":1}}],
     "warnings":[]}
    """;
var cases = new Dictionary<string,string> {
    ["malformed"] = "{", ["wrong document"] = input.Replace(id.ToString(), Guid.NewGuid().ToString()),
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
