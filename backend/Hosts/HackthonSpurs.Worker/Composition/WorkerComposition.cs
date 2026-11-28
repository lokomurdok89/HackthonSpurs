using HackthonSpurs.Application.Embeddings;
using HackthonSpurs.Application.Processing;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Persistence.Processing;
using HackthonSpurs.Resources;
using HackthonSpurs.Worker.Indexing;
using HackthonSpurs.Worker.Ocr;
using HackthonSpurs.Worker.Ocr.Rendering;
using HackthonSpurs.Worker.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace HackthonSpurs.Worker.Composition;

internal static class WorkerComposition
{
    /// <summary>Register shared persistence and exactly one of the document and index lanes.</summary>
    /// <param name="builder">Worker builder supplying services, configuration and hosting settings.</param>
    /// <returns>True when the index lane was selected and needs a provider validation at startup.</returns>
    internal static bool Register(HostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var connection = config.GetConnectionString(SettingNames.DocumentConnection)
            ?? throw new InvalidOperationException(ResourceText.ConnectionRequired);
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));

        if (config.GetValue<bool>(SettingNames.IndexWorker))
        {
            SpurEmbeddingSettings.Register(builder.Services, config, builder.Environment.IsEnvironment(SettingNames.TestingEnvironment));
            builder.Services.AddSingleton<IIndexJobs, PostgresIndexJobs>();
            builder.Services.AddSingleton<ProcessIndexJob>();
            builder.Services.AddHostedService<IndexWorker>();
            return true;
        }

        RegisterDocumentLane(builder.Services, config);
        return false;
    }

    /// <summary>Reject index startup when the selected lane has no configured embedding provider.</summary>
    /// <param name="host">Built worker with the lane's registered options.</param>
    /// <param name="indexLane">Whether the index lane was registered.</param>
    internal static void Validate(IHost host, bool indexLane)
    {
        if (indexLane && !host.Services.GetRequiredService<EmbeddingProfile>().IsConfigured)
            throw new InvalidOperationException(ResourceText.RequireIndexing);
    }

    /// <summary>Configure one document queue, its private reader and the corresponding processing limits.</summary>
    /// <param name="services">Host registrations for the document lane.</param>
    /// <param name="config">Settings for originals, the private reader and OCR budgets.</param>
    private static void RegisterDocumentLane(IServiceCollection services, IConfiguration config)
    {
        var root = config[SettingNames.StorageRoot] ?? "";
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException(ResourceText.RequireRoot);

        // Restrict each worker to its private reader and disjoint queue filter.
        var endpoint = new Uri(config[SettingNames.ReaderEndpoint] ?? throw new InvalidOperationException(ResourceText.RequireReader));
        bool spur = endpoint == new Uri(ServiceDefaults.SpurReader);
        if (!spur && endpoint != new Uri(ServiceDefaults.FixtureReader))
            throw new InvalidOperationException(ResourceText.UnsupportedReader);
        services.AddSingleton<IReadingJobs>(sp =>
            new PostgresReadingJobs(sp.GetRequiredService<NpgsqlDataSource>(), spur, FixtureLane.Hash));
        services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
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
        services.AddSingleton<IDocumentReader>(sp => new ReaderClient(sp.GetRequiredService<HttpClient>(), endpoint, root, bands));
        services.AddSingleton<ProcessReadingJob>();
        services.AddHostedService<DocumentWorker>();
    }
}
