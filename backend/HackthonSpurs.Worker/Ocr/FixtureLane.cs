using System.Security.Cryptography;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Worker.Ocr;

internal static class FixtureLane
{
    internal static readonly string Hash = ComputeHash();

    /// <summary>Compute the allowlisted digest from the embedded synthetic test original.</summary>
    private static string ComputeHash()
    {
        using var fixture = typeof(FixtureLane).Assembly.GetManifestResourceStream(Wire.FixtureResource)!;
        return Convert.ToHexStringLower(SHA256.HashData(fixture));
    }
}
