using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Http;

internal static class WorkspaceRequestGuard
{
    /// <summary>Require the same-origin custom header on unsafe methods; never enable cross-origin preflight.</summary>
    /// <param name="context">Current HTTP exchange.</param>
    /// <param name="next">Next middleware in the pipeline.</param>
    public static Task RequireWorkspaceHeaderAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method) ||
            (request.Headers.TryGetValue(Wire.WorkspaceRequestHeader, out var header) &&
                header.Count == 1 && header[0] == Wire.WorkspaceRequestValue))
            return next(context);
        return ApiProblems.Error(Errors.WorkspaceRequestRequired).ExecuteAsync(context);
    }
}
