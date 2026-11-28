using HackthonSpurs.Resources;
using HackthonSpurs.Reader.Fixture;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(Wire.AspNetLogCategory, LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = Policy.MaxFixtureBytes);
var app = builder.Build();
await FixtureEndpoint.MapAsync(app);
await app.RunAsync();
