using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackthonSpurs.SpurReader;
using HackthonSpurs.SpurReader.Tests;
using Microsoft.AspNetCore.Http;

// Real endpoint + adapter, fake HTTP transport. No socket, real key or document.
const string endpoint = "https://ai.spuric.com/v1/chat/completions";
var env = new Dictionary<string, string>
{
    ["SPUR_OCR_ENDPOINT"] = endpoint,
    ["SPUR_API_KEY"] = "invented-test-key",
    ["SPUR_OCR_MODEL"] = "test-vision-alias"
};
foreach (var name in env.Keys)
    Reject(() => SpurSettings.FromEnvironment(k => k == name ? " " : env[k]));
foreach (var bad in new[] { "http://ai.spuric.com/v1/chat/completions", "https://example.invalid/v1/chat/completions",
    endpoint + "?key=x", endpoint + "#fragment", endpoint.Replace("//", "//user@") })
    Reject(() => SpurSettings.FromEnvironment(k => k == "SPUR_OCR_ENDPOINT" ? bad : env[k]));
Reject(() => SpurSettings.FromEnvironment(k => k == "SPUR_API_KEY" ? "abc\nprivate" : env[k]));
var settings = SpurSettings.FromEnvironment(k => env[k]);
var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/k2kAAAAASUVORK5CYII=");
var doc = Guid.NewGuid();
var fake = new FakeGateway();
using var http = new HttpClient(fake) { MaxResponseContentBufferSize = 128 * 1024 };
var client = new SpurVisionClient(http, settings);
// Construct a private reader request using only the test image.
// media: Declared test image type.
HttpRequest Request(string media = "image/png")
{
    var r = new DefaultHttpContext().Request;
    r.Headers["X-Reader-Contract"] = "1";
    r.Headers["X-Document-Id"] = doc.ToString("D");
    r.Headers["X-Page-Number"] = "7";
    r.ContentType = media; r.ContentLength = bytes.Length; r.Body = new MemoryStream(bytes);
    return r;
}
// Invoke the page endpoint against the in-memory gateway.
// r: Synthetic request for the private reader.
Task<IResult> Read(HttpRequest r) => PageEndpoint.Read(r, client, default);
// Read the HTTP status from the endpoint result.
// r: Endpoint response under test.
static int? Status(IResult r) => ((IStatusCodeHttpResult)r).StatusCode;
// Serialize the endpoint result value for contract assertions.
// r: Endpoint response under test.
static JsonElement Value(IResult r) => JsonSerializer.SerializeToElement(((IValueHttpResult)r).Value);
// Fail the offline check when its assertion is false.
// passed: Assertion result.
// label: Diagnostic identifying the assertion.
static void Check(bool passed, string label) { if (!passed) throw new Exception(label); }
// Require invalid reader configuration to raise a configuration exception.
// action: Configuration operation expected to fail.
static void Reject(Action action)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Invalid settings accepted.");
}

fake.Inspect = async req =>
{
    Check(req.Method == HttpMethod.Post && req.RequestUri!.AbsoluteUri == endpoint, "Wrong request target");
    Check(req.Headers.Authorization?.ToString() == "Bearer invented-test-key", "Missing Bearer header");
    Check(req.Content!.Headers.ContentType?.MediaType == "application/json", "Not JSON");
    var body = await req.Content.ReadFromJsonAsync<JsonElement>();
    var message = body.GetProperty("messages")[0];
    var parts = message.GetProperty("content");
    Check(message.GetProperty("role").GetString() == "user" && parts[0].GetProperty("type").GetString() == "text" &&
        parts[1].GetProperty("type").GetString() == "image_url" && body.GetProperty("model").GetString() == env["SPUR_OCR_MODEL"], "Wrong model/message");
    Check(parts[1].GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64," + Convert.ToBase64String(bytes), "Wrong image");
    var raw = body.GetRawText();
    Check(!raw.Contains(doc.ToString()) && !raw.Contains(env["SPUR_API_KEY"]), "Local identity/key leaked in body");
};
var ok = Value(await Read(Request()));
var f = ok.GetProperty("fragments")[0];
Check(ok.GetProperty("readerVersion").GetString() == "spur/1:test-vision-alias", "Reader version must identify the configured model");
Check(ok.GetProperty("documentId").GetGuid() == doc && f.GetProperty("text").GetString() == "DEMO" &&
    f.GetProperty("method").GetString() == "ocr" && f.GetProperty("locator").GetProperty("page").GetInt32() == 7, "Lost text or locator");
fake.Inspect = null;
fake.Reply = () => FakeGateway.Message(" ");
var empty = Value(await Read(Request()));
Check(empty.GetProperty("fragments").GetArrayLength() == 0 && empty.GetProperty("warnings")[0].GetProperty("locator").GetProperty("page").GetInt32() == 7, "Empty page mislabeled");

var before = fake.Calls;
var oversize = Request(); oversize.ContentLength = PageEndpoint.MaxBytes + 1;
var badBytes = Request(); badBytes.Body = new MemoryStream(new byte[bytes.Length]);
var badPage = Request(); badPage.Headers["X-Page-Number"] = "0";
var missingLength = Request(); missingLength.ContentLength = null;
foreach (var (request, status) in new[] { (Request("application/pdf"), 415), (badBytes, 415), (badPage, 400), (oversize, 413), (missingLength, 411) })
    Check(Status(await Read(request)) == status, "Input not rejected");
Check(fake.Calls == before, "Invalid input reached gateway");

foreach (var (reply, code) in new (Func<HttpResponseMessage>, string)[] {
    (() => new(HttpStatusCode.Unauthorized) { Content = new StringContent("private diagnostic") }, "reader_credentials_rejected"),
    (() => new(HttpStatusCode.TooManyRequests), "reader_unavailable"), (() => new(HttpStatusCode.ServiceUnavailable), "reader_unavailable"),
    (() => new(HttpStatusCode.Redirect), "reader_request_rejected"),
    (() => FakeGateway.Message("partial", "length"), "invalid_reader_result"),
    (() => FakeGateway.Message(new string('x', 140000)), "invalid_reader_result"),
    (() => new(HttpStatusCode.OK) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") }, "invalid_reader_result"),
    (() => throw new HttpRequestException("private diagnostic"), "reader_unavailable"),
    (() => throw new TaskCanceledException("private diagnostic"), "reader_timeout") })
{
    fake.Reply = reply;
    var result = Value(await Read(Request()));
    Check(result.GetProperty("code").GetString() == code && !result.GetRawText().Contains("private diagnostic"), "Unsafe failure");
}
Console.WriteLine("PASS: settings isolation, multimodal request, page/empty results, admission and classified provider failures (offline).");
