using HackthonSpurs.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;
var connection = config.GetConnectionString("Documents") ?? throw new InvalidOperationException("Missing Documents connection.");
var root = config["Storage:Root"] ?? "";
if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("An absolute originals directory is required.");

// This slice has no general-purpose or external OCR transport.
var endpoint = new Uri(config["Reader:Endpoint"] ?? throw new InvalidOperationException("Reader endpoint required."));
if (endpoint != new Uri("http://fixture-reader:8080/internal/read"))
    throw new InvalidOperationException("Only the internal fixture reader is supported.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler {
    AllowAutoRedirect = false, UseProxy = false, UseCookies = false
}) { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 65536 });
builder.Services.AddSingleton(sp => new ReaderClient(sp.GetRequiredService<HttpClient>(), endpoint, root));
builder.Services.AddHostedService<DocumentWorker>();
await builder.Build().RunAsync();
