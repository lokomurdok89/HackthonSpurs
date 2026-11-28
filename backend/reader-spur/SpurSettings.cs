using HackthonSpurs.Resources;
using System.Net.Http.Headers;

namespace HackthonSpurs.SpurReader;

// A class, not a positional record: diagnostic ToString() must never include the key.
internal sealed class SpurSettings
{
    /// <summary>Construct settings only through the validated environment factory.</summary>
    private SpurSettings() { }

    public required Uri Endpoint { get; init; }
    public required string Model { get; init; }
    public required AuthenticationHeaderValue Authorization { get; init; }

    /// <summary>Validate the official endpoint, model identifier and bearer credential.</summary>
    /// <param name="read">Lookup for a named environment setting.</param>
    public static SpurSettings FromEnvironment(Func<string, string?> read)
    {
        // Read a required setting without disclosing its value on failure.
        // name: Required setting name.
        string Required(string name)
        {
            var value = read(name);
            return !string.IsNullOrWhiteSpace(value) ? value :
                throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, ResourceText.RequiredSetting, name));
        }
        if (!Uri.TryCreate(Required(SettingNames.VisionEndpointEnv), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.Host != ServiceDefaults.GatewayHost
            || uri.AbsolutePath != ServiceDefaults.VisionPath || uri.UserInfo.Length + uri.Query.Length + uri.Fragment.Length != 0)
            throw new InvalidOperationException(ResourceText.InvalidVisionEndpoint);

        var model = Required(SettingNames.VisionModelEnv);
        if (model.Length > Policy.MaxModelLength || !model.All(c => char.IsAsciiLetterOrDigit(c) || ServiceDefaults.ModelPunctuation.Contains(c)))
            throw new InvalidOperationException(ResourceText.InvalidVisionModel);
        var key = Required(SettingNames.ApiKeyEnv);
        if (key.Any(c => c < '!' || c > '~'))
            throw new InvalidOperationException(ResourceText.InvalidApiKey);
        return new() { Endpoint = uri, Model = model, Authorization = new(Wire.Bearer, key) };
    }
}
