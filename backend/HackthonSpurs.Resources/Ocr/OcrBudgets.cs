using System.Globalization;

namespace HackthonSpurs.Resources;

/// <summary>Validate OCR payload settings identically at the preparing and sending boundaries.</summary>
public static class OcrBudgets
{
    /// <summary>Read the optional image target in bytes from value; never expand the private wire limit.</summary>
    public static int ImageTarget(string? value) => Math.Min(Policy.DefaultImageTarget, Parse(value, Policy.DefaultImageTarget, Policy.MaxImageBytes));

    /// <summary>Read the optional serialized UTF-8 request limit in bytes from value.</summary>
    public static int JsonLimit(string? value) => Parse(value, Policy.DefaultJsonLimit, Policy.MaxJsonLimit);

    /// <summary>Use fallback for an absent value; otherwise require a positive decimal not exceeding maximum.</summary>
    private static int Parse(string? value, int fallback, int maximum) => value is null ? fallback :
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 && n <= maximum
            ? n : throw new InvalidOperationException(ResourceText.InvalidOcrBudget);
}
