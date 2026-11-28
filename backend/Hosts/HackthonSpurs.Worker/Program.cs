using HackthonSpurs.Worker.Composition;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var indexLane = WorkerComposition.Register(builder);
using var host = builder.Build();
WorkerComposition.Validate(host, indexLane);
await host.RunAsync();
