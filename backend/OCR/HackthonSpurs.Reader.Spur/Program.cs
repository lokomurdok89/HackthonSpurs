using HackthonSpurs.Reader.Spur.Contracts;
using HackthonSpurs.Reader.Spur.Endpoints;
using HackthonSpurs.Reader.Spur.Provider;
using HackthonSpurs.Resources;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(Wire.AspNetLogCategory, LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = PageEndpoint.MaxBytes);
builder.Services.AddSingleton(SpurSettings.FromEnvironment(Environment.GetEnvironmentVariable));
builder.Services.AddSingleton(_ => SpurVisionClient.CreateHttpClient());
builder.Services.AddSingleton<IPageTranscriber, SpurVisionClient>();
var app = builder.Build();
app.MapPost(Wire.PrivateRead, PageEndpoint.Read);
await app.RunAsync();
