using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Endpoints.Documents;

internal static class DocumentEndpoints
{
    /// <summary>Map document commands and read-only receipt operations with their existing access rules.</summary>
    /// <param name="app">Workspace API endpoint builder.</param>
    public static void Map(WebApplication app)
    {
        var documents = app.MapGroup(Wire.Documents);
        documents.MapPost("", UploadEndpoint.Handle).RequireAuthorization(Wire.AdminRole);
        documents.MapGet(Wire.ById, DocumentReadEndpoints.Status);
        documents.MapGet(Wire.Result, DocumentReadEndpoints.Result);
        documents.MapGet(Wire.IndexStatus, IndexInspectionEndpoints.Status);
        documents.MapGet(Wire.Chunks, IndexInspectionEndpoints.Chunks);
        documents.MapPost(Wire.Reindex, IndexInspectionEndpoints.Reindex).RequireAuthorization(Wire.AdminRole);
    }
}
