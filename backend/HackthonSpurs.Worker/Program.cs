using HackthonSpurs.Resources;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;
var connection = config.GetConnectionString(SettingNames.DocumentConnection) ?? throw new InvalidOperationException(ResourceText.ConnectionRequired);
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));

if (config.GetValue<bool>(SettingNames.IndexWorker))
{
    SpurEmbeddingSettings.Register(builder.Services, config, builder.Environment.IsEnvironment(SettingNames.TestingEnvironment));
    builder.Services.AddSingleton<IndexQueue>();
    builder.Services.AddHostedService<IndexWorker>();
    using var host = builder.Build();
    if (!host.Services.GetRequiredService<SpurEmbeddingSettings>().IsConfigured)
        throw new InvalidOperationException(ResourceText.RequireIndexing);
    await host.RunAsync();
    return;
}

var root = config[SettingNames.StorageRoot] ?? "";
if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException(ResourceText.RequireRoot);

// Restrict each worker to its private reader and disjoint queue filter.
var endpoint = new Uri(config[SettingNames.ReaderEndpoint] ?? throw new InvalidOperationException(ResourceText.RequireReader));
bool spur = endpoint == new Uri(ServiceDefaults.SpurReader);
if (!spur && endpoint != new Uri(ServiceDefaults.FixtureReader))
    throw new InvalidOperationException(ResourceText.UnsupportedReader);
builder.Services.AddSingleton(sp => new JobQueue(sp.GetRequiredService<NpgsqlDataSource>(), spur));
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseProxy = false,
    UseCookies = false
})
{ Timeout = TimeSpan.FromSeconds(Policy.ReaderSeconds), MaxResponseContentBufferSize = spur ? Policy.MaxPageReply : Policy.MaxFixtureReply });
var target = spur ? OcrBudgets.ImageTarget(config[SettingNames.OcrImageTarget]) : Policy.MaxFixtureBytes;
var bands = new BandPlanner(target, OcrBudgets.JsonLimit(config[SettingNames.OcrJsonLimit]),
    config[SettingNames.VisionModelEnv] ?? (spur ? throw new InvalidOperationException(ResourceText.InvalidVisionModel) : string.Empty),
    config.GetValue(SettingNames.OcrAdaptiveMonochrome, true));
builder.Services.AddSingleton(sp => new ReaderClient(sp.GetRequiredService<HttpClient>(), endpoint, root, bands));
builder.Services.AddHostedService<DocumentWorker>();
await builder.Build().RunAsync();
