using HackthonSpurs.Resources;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackthonSpurs.Documents.Embeddings;

/// <summary>Define an explicit embedding profile shared by indexing and search.</summary>
/// <param name="Endpoint">Allowed embeddings endpoint, or null when disabled.</param>
/// <param name="ApiKey">Server-only provider authorization value.</param>
/// <param name="Model">Provider model identifier sent verbatim.</param>
/// <param name="Version">Operator-managed vector-space revision, never supplied by callers.</param>
/// <param name="Dimensions">Number of components expected per vector.</param>
public sealed class SpurEmbeddingSettings(Uri? Endpoint, string ApiKey, string Model, string Version, int Dimensions)
{
    public Uri? Endpoint { get; } = Endpoint;
    internal string Key { get; } = ApiKey;
    public string ModelId { get; } = Model;
    public string ProfileVersion { get; } = Version;
    public int VectorDimensions { get; } = Dimensions;
    public bool IsConfigured => Endpoint is not null;

    /// <summary>Register the bounded embedding client; reject a configured but invalid destination or profile.</summary>
    /// <param name="services">Host DI registration collection.</param>
    /// <param name="config">Server-only configuration with the optional Spur settings.</param>
    /// <param name="allowTestEndpoint">Whether the isolated test host may call the named private stub.</param>
    public static void Register(IServiceCollection services, IConfiguration config, bool allowTestEndpoint)
    {
        string model = config[SettingNames.EmbedModel] is { Length: > 0 } embedModel ? embedModel : ServiceDefaults.EmbedModel;
        string version = config[SettingNames.EmbedVersion] is { Length: > 0 } embedVersion ? embedVersion : ServiceDefaults.EmbedVersion;
        int dimensions = string.IsNullOrEmpty(config[SettingNames.EmbedDimensions])
            ? ServiceDefaults.EmbedDimensions : config.GetValue<int>(SettingNames.EmbedDimensions);
        string key = config[SettingNames.ApiKey] ?? "";
        Uri? endpoint = null;
        if (config.GetValue<bool>(SettingNames.EmbeddingsEnabled))
        {
            string url = (config[SettingNames.BaseUrl] is { Length: > 0 } baseUrl ? baseUrl : ServiceDefaults.Gateway).TrimEnd('/');
            if (!ValidProfilePart(model) || !ValidProfilePart(version) || dimensions is < 1 or > Policy.MaxDimensions ||
                key.Length is < 1 or > Policy.MaxKeyLength || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')) ||
                !AuthenticationHeaderValue.TryParse(Wire.Bearer + " " + key, out _) ||
                (url != ServiceDefaults.Gateway && !(allowTestEndpoint && url == ServiceDefaults.TestGateway)))
                throw new InvalidOperationException(ResourceText.InvalidEmbedConfiguration);
            endpoint = new Uri(url + ServiceDefaults.EmbeddingsPath);
        }
        services.AddSingleton(new SpurEmbeddingSettings(endpoint, endpoint is null ? "" : key, model, version, dimensions));
        services.AddHttpClient<SpurEmbeddingClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(Policy.ProviderSeconds);
                client.MaxResponseContentBufferSize = Policy.MaxEmbedReply;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseProxy = false, UseCookies = false
            })
            .RemoveAllLoggers();
    }

    /// <summary>Validate a bounded profile component used in public metadata and provider routing.</summary>
    /// <param name="value">Configured model or version token.</param>
    private static bool ValidProfilePart(string value) => value.Length is > 0 and <= Policy.MaxModelLength &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || ServiceDefaults.ModelPunctuation.Contains(c));
}

public sealed class SpurEmbeddingException : Exception
{
    /// <summary>Classify a provider failure without retaining the payload or upstream exception.</summary>
    /// <param name="error">Catalog definition of the classified failure.</param>
    /// <param name="retryable">Whether the durable indexing worker can retry this failure.</param>
    internal SpurEmbeddingException(BackendError error, bool retryable = false) : base(error.Code)
    {
        Error = error;
        Retryable = retryable;
    }

    public BackendError Error { get; }
    public string Code => Error.Code;
    public int StatusCode => Error.HttpStatus!.Value;
    public bool Retryable { get; }
}
