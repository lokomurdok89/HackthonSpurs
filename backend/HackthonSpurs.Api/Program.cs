using HackthonSpurs.Resources;
using HackthonSpurs.Api;
using HackthonSpurs.Api.Auth;
using HackthonSpurs.Documents.Commands;
using HackthonSpurs.Documents.Embeddings;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Documents.Queries;
using HackthonSpurs.Documents.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddSingleton<OriginalStore>();
builder.Services.AddScoped<AcceptDocumentHandler>();
builder.Services.AddScoped<GetDocumentStatusHandler>();
builder.Services.AddScoped<GetDocumentResultHandler>();
builder.Services.AddScoped<GetDocumentIndexStatusHandler>();
builder.Services.AddScoped<DocumentSearchService>();
SpurEmbeddingSettings.Register(builder.Services, builder.Configuration, builder.Environment.IsEnvironment(SettingNames.TestingEnvironment));
WorkspaceSecurity.Register(builder.Services);

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DocumentsDb>().Database.MigrateAsync();
    await WorkspaceSecurity.BootstrapAsync(scope.ServiceProvider, app.Configuration, app.Lifetime.ApplicationStopping);
}

// Mark document responses private to the request and sanitize persistence outages.
// context: Current HTTP exchange. next: The remaining request pipeline.
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = Wire.CachePolicy;
    try { await next(context); }
    catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested &&
        IsPersistenceOrStorageFailure(ex))
    {
        app.Logger.LogWarning(ResourceText.ApiOutage, context.TraceIdentifier);
        await ApiProblems.Unavailable().ExecuteAsync(context);
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.Use(WorkspaceSecurity.RequireWorkspaceHeaderAsync);
app.UseRateLimiter();
WorkspaceEndpoints.Map(app);

var documents = app.MapGroup(Wire.Documents);
documents.MapPost("", UploadEndpoint.Handle).RequireAuthorization(Wire.AdminRole);
documents.MapGet(Wire.ById, DocumentQueries.Status);
documents.MapGet(Wire.Result, DocumentQueries.Result);
documents.MapGet(Wire.IndexStatus, SearchEndpoints.Status);

var search = app.MapGroup(Wire.Search);
search.MapPost("", SearchEndpoints.Search);
search.MapGet(Wire.Stats, SearchEndpoints.Stats);
await app.RunAsync();

// Recognize wrapped database and file failures that map to temporary unavailability.
// error: Exception and inner causes to inspect.
static bool IsPersistenceOrStorageFailure(Exception error)
{
    // EF's execution strategy can wrap transient provider exceptions in InvalidOperationException.
    // An outage is a retryable HTTP failure, not a terminal document-processing failure.
    for (Exception? cause = error; cause is not null; cause = cause.InnerException)
        if (cause is NpgsqlException or DbUpdateException or IOException or UnauthorizedAccessException or TimeoutException)
            return true;
    return false;
}
