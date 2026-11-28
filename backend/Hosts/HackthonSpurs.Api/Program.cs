using HackthonSpurs.Api.Composition;
using HackthonSpurs.Api.Endpoints.Documents;
using HackthonSpurs.Api.Endpoints.Search;
using HackthonSpurs.Api.Endpoints.Workspace;
using HackthonSpurs.Api.Http;

var builder = WebApplication.CreateBuilder(args);
ApiComposition.Register(builder);

var app = builder.Build();
await ApiComposition.InitializeAsync(app);
ApiPipeline.Configure(app);
WorkspaceEndpoints.Map(app);
DocumentEndpoints.Map(app);
SearchEndpoints.Map(app);
await app.RunAsync();
