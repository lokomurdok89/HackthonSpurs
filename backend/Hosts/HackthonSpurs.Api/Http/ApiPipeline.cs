using HackthonSpurs.Resources;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HackthonSpurs.Api.Http;

internal static class ApiPipeline
{
    /// <summary>Configure private responses, sanitized outages and workspace HTTP middleware in order.</summary>
    /// <param name="app">API whose request pipeline is being configured.</param>
    internal static void Configure(WebApplication app)
    {
        // Mark document responses private and sanitize persistence outages.
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
        app.Use(WorkspaceRequestGuard.RequireWorkspaceHeaderAsync);
        app.UseRateLimiter();
    }

    /// <summary>Recognize wrapped database and file failures that map to temporary unavailability.</summary>
    /// <param name="error">Exception and inner causes to inspect.</param>
    private static bool IsPersistenceOrStorageFailure(Exception error)
    {
        // EF's execution strategy can wrap transient provider exceptions in InvalidOperationException.
        // An outage is a retryable HTTP failure, not a terminal document-processing failure.
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
            if (cause is NpgsqlException or DbUpdateException or IOException or UnauthorizedAccessException or TimeoutException)
                return true;
        return false;
    }
}
