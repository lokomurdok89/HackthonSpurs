using System.Threading.RateLimiting;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HackthonSpurs.Api.Auth;

internal static class WorkspaceSecurity
{
    /// <summary>Register database-backed identity, default workspace authorization and sign-in throttling.</summary>
    /// <param name="services">Application services being configured.</param>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = Policy.PasswordHashIterations);
        services.AddSingleton<IPasswordHasher<WorkspaceUser>, PasswordHasher<WorkspaceUser>>();
        services.AddSingleton<DummyCredential>();
        services.AddScoped<WorkspaceSessions>();
        services.AddScoped<WorkspaceUsers>();
        services.AddAuthentication(Wire.WorkspaceScheme)
            .AddScheme<AuthenticationSchemeOptions, WorkspaceAuthentication>(Wire.WorkspaceScheme, _ => { });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Wire.AdminRole, policy => policy.RequireAuthenticatedUser().RequireRole(Wire.AdminRole));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Wire.Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Policy.LoginPermits,
                    Window = TimeSpan.FromSeconds(Policy.LoginWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellation) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(delay.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await ApiProblems.Error(Errors.LoginRateLimited).ExecuteAsync(context.HttpContext);
            };
        });
    }

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

    /// <summary>Provision the initial active admin only on an empty migrated workspace.</summary>
    /// <param name="services">Scope containing the migrated context and password hasher.</param>
    /// <param name="configuration">Server-only configuration with optional bootstrap values.</param>
    /// <param name="ct">Application startup cancellation.</param>
    public static async Task BootstrapAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct)
    {
        _ = services.GetRequiredService<DummyCredential>();
        var configuredUsername = configuration[SettingNames.BootstrapUsername];
        var password = configuration[SettingNames.BootstrapPassword];
        if (string.IsNullOrEmpty(configuredUsername) && string.IsNullOrEmpty(password)) return;
        var db = services.GetRequiredService<DocumentsDb>();
        await using var transaction = await WorkspaceMappings.LockWritersAsync(db, ct);
        if (await db.WorkspaceUsers.AnyAsync(ct)) return;
        var username = AccountFields.Username(configuredUsername);
        if (username is null || !AccountFields.ValidPassword(password))
            throw new InvalidOperationException(ResourceText.InvalidBootstrap);
        var hasher = services.GetRequiredService<IPasswordHasher<WorkspaceUser>>();
        db.WorkspaceUsers.Add(AccountFields.Create(username, username, password!, Wire.AdminRole, hasher));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
