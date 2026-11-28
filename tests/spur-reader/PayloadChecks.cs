using System.Globalization;
using HackthonSpurs.Reader.Spur.Contracts;
using HackthonSpurs.Reader.Spur.Provider;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Reader.Spur.Tests;

internal static class PayloadChecks
{
    /// <summary>Exercise image and actual UTF-8 envelope boundaries with the invented env and image.</summary>
    internal static async Task VerifyAsync(Dictionary<string, string> env, byte[] image)
    {
        var gateway = new FakeGateway();
        using var http = new HttpClient(gateway);
        long length = 0;
        gateway.Inspect = async req =>
        {
            length = (await req.Content!.ReadAsByteArrayAsync()).Length;
            if (req.Content.Headers.ContentLength != length) throw new Exception("Unexpected wire length.");
        };

        // Submit one synthetic image with the specified raw and JSON byte budgets, returning only the safe failure code.
        async Task<string?> Run(int raw, int json)
        {
            var settings = SpurSettings.FromEnvironment(k => k switch
            {
                SettingNames.OcrImageTarget => raw.ToString(CultureInfo.InvariantCulture),
                SettingNames.OcrJsonLimit => json.ToString(CultureInfo.InvariantCulture),
                _ => env.GetValueOrDefault(k)
            });
            var client = new SpurVisionClient(http, settings);
            try { await client.TranscribeAsync(image, Media.Png, default); return null; }
            catch (ReaderFailure ex) { return ex.Error.Code; }
        }
        if (await Run(image.Length, 10000) != null || length <= image.Length) throw new Exception("Valid payload rejected.");
        int exact = checked((int)length);
        if (await Run(image.Length, exact) != null ||
            await Run(image.Length, exact - 1) != Errors.OcrJsonOverBudget.Code ||
            await Run(image.Length - 1, exact) != Errors.OcrPageOverBudget.Code || gateway.Calls != 2)
            throw new Exception("Incorrect boundary or unexpected outbound call.");
        foreach (var bad in new[] { "", "0", "-1", "bad", "999999999" })
        {
            try { OcrBudgets.ImageTarget(bad); }
            catch (InvalidOperationException) { continue; }
            throw new Exception("Invalid image budget accepted.");
        }
        Console.WriteLine($"PASS: exact UTF-8 boundary {exact} bytes; over-budget pages made zero gateway calls.");
    }
}
