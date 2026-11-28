namespace HackthonSpurs.Application.Tests;

internal static class Check
{
    /// <summary>Throw when an offline test expectation is not met.</summary>
    /// <param name="condition">Result of the expected behavior comparison.</param>
    /// <param name="message">Diagnostic describing the failed expectation.</param>
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
