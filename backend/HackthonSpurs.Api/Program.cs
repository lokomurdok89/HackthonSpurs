using HackthonSpurs.Api;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Documents")
    ?? throw new InvalidOperationException("ConnectionStrings:Documents is required.");
var maxBytes = builder.Configuration.GetValue<long>("Storage:MaxFileBytes");
var root = builder.Configuration["Storage:Root"];
if (maxBytes is <= 0 or > 1024L * 1024 * 1024 || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
    throw new InvalidOperationException("Provide an absolute Storage:Root and a valid Storage:MaxFileBytes (1 byte to 1 GiB).");

var spurSettings = new SpurEmbeddingSettings(
    builder.Configuration["Spur:BaseUrl"] ?? "https://ai.spuric.com/v1",
    builder.Configuration["Spur:ApiKey"] ?? "",
    builder.Configuration["Spur:EmbedModel"] ?? "bge-m3");

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxBytes + 65536); // includes multipart framing
builder.Services.AddDbContext<DocumentsDb>(options => options.UseNpgsql(connection, o => o.UseVector()));
builder.Services.AddSingleton(new StorageSettings(root, maxBytes));
builder.Services.AddSingleton(spurSettings);
builder.Services.AddSingleton<OriginalStore>();
builder.Services.AddSingleton<EmbeddingWorkQueue>();
builder.Services.AddScoped<Ingestion>();
builder.Services.AddHttpClient<SpurEmbeddingClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<DocumentSearchService>();
builder.Services.AddHostedService<DocumentEmbeddingWorker>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<DocumentsDb>().Database.MigrateAsync();

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    try { await next(context); }
    catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested &&
        IsPersistenceOrStorageFailure(ex))
    {
        app.Logger.LogWarning("Document service unavailable. Trace: {TraceId}", context.TraceIdentifier);
        await ApiProblems.Unavailable().ExecuteAsync(context);
    }
});

var documents = app.MapGroup("/api/documents");
documents.MapPost("", UploadEndpoint.Handle);
documents.MapGet("/{id}", DocumentQueries.Status);
documents.MapGet("/{id}/result", DocumentQueries.Result);
documents.MapPost("/{id}/index", SearchEndpoints.Index);

var search = app.MapGroup("/api/search");
search.MapPost("", SearchEndpoints.Search);
search.MapGet("/stats", SearchEndpoints.Stats);

await app.RunAsync();

static bool IsPersistenceOrStorageFailure(Exception error)
{
    // EF's execution strategy can wrap transient provider exceptions in InvalidOperationException.
    // An outage is a retryable HTTP failure, not a terminal document-processing failure.
    for (Exception? cause = error; cause is not null; cause = cause.InnerException)
        if (cause is NpgsqlException or DbUpdateException or IOException or UnauthorizedAccessException or TimeoutException)
            return true;
    return false;
}
