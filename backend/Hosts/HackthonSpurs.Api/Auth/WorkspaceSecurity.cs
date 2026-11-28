using System.Threading.RateLimiting;
using HackthonSpurs.Api.Auth.Accounts;
using HackthonSpurs.Api.Auth.Sessions;
using HackthonSpurs.Api.Http;
using HackthonSpurs.Application.Commands;
using HackthonSpurs.Application.Queries;
using HackthonSpurs.Documents.Persistence.Queries;
using HackthonSpurs.Documents.Persistence;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

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
        services.AddScoped<WorkspaceSessionLookup>();
        services.AddScoped<WorkspaceAccountCommands>();
        services.AddScoped<IWorkspaceAccountWriter, WorkspaceAccountWriter>();
        services.AddScoped<IWorkspaceAccountReads, PostgresWorkspaceAccountReads>();
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
}
