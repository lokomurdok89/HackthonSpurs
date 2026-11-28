using HackthonSpurs.Api.Auth;
using HackthonSpurs.Api.Auth.Accounts;
using HackthonSpurs.Application.Commands;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Documents.Persistence.Commands;
using HackthonSpurs.Documents.Persistence.Queries;
using HackthonSpurs.Documents.Storage;
using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Composition;

internal static class ApiComposition
{
    /// <summary>Register document use cases, storage, persistence and workspace authentication.</summary>
    /// <param name="builder">API builder supplying services, configuration and hosting settings.</param>
    internal static void Register(WebApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString(SettingNames.DocumentConnection)
            ?? throw new InvalidOperationException(ResourceText.ConnectionRequired);
        var maxBytes = string.IsNullOrEmpty(builder.Configuration[SettingNames.MaxFileBytes])
            ? Policy.MaxDocumentBytes : builder.Configuration.GetValue<long>(SettingNames.MaxFileBytes);
        var root = builder.Configuration[SettingNames.StorageRoot];
        if (maxBytes is <= 0 or > Policy.MaxStorageBytes || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException(ResourceText.StorageInvalid);

        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxBytes + Policy.MultipartOverhead); // includes multipart framing
        builder.Services.AddDbContext<DocumentsDb>(options => options.UseNpgsql(connection, npgsql => npgsql.UseVector()));
        builder.Services.AddSingleton(new StorageSettings(root, maxBytes));
        builder.Services.AddSingleton<IOriginalStaging, OriginalStore>();
        builder.Services.AddScoped<AcceptDocumentHandler>();
        builder.Services.AddScoped<IDocumentAdmissions, PostgresDocumentAdmissions>();
        builder.Services.AddScoped<IDocumentReads, PostgresDocumentReads>();
        builder.Services.AddScoped<ITranscriptReindex, PostgresTranscriptReindex>();
        builder.Services.AddScoped<PostgresSemanticIndex>();
        builder.Services.AddScoped<IIndexedDocumentReads>(sp => sp.GetRequiredService<PostgresSemanticIndex>());
        builder.Services.AddScoped<ISemanticSearchIndex>(sp => sp.GetRequiredService<PostgresSemanticIndex>());
        builder.Services.AddScoped<DocumentSearchService>();
        builder.Services.AddScoped<ReindexTranscriptHandler>();
        SpurEmbeddingSettings.Register(builder.Services, builder.Configuration, builder.Environment.IsEnvironment(SettingNames.TestingEnvironment));
        WorkspaceSecurity.Register(builder.Services);
    }

    /// <summary>Apply existing migrations before account provisioning or accepting requests.</summary>
    /// <param name="app">Built API with its root services, configuration and lifetime.</param>
    internal static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DocumentsDb>().Database.MigrateAsync();
        await WorkspaceBootstrap.BootstrapAsync(scope.ServiceProvider, app.Configuration, app.Lifetime.ApplicationStopping);
    }
}
