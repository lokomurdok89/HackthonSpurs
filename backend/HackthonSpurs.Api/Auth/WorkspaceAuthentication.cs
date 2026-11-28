using System.Security.Claims;
using System.Text.Encodings.Web;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HackthonSpurs.Api.Auth;

/// <summary>Authenticate workspace requests by looking up the opaque session in PostgreSQL.</summary>
/// <param name="options">Framework scheme settings.</param>
/// <param name="logger">Framework authentication logger.</param>
/// <param name="encoder">Framework URL encoder.</param>
/// <param name="sessions">Per-request workspace session service.</param>
internal sealed class WorkspaceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, WorkspaceSessions sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Load the authenticated user and keep only the public account summary in request scope.</summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = await sessions.FindUserAsync(SessionTokens.Read(Request), Context.RequestAborted);
        if (user is null) return AuthenticateResult.NoResult();
        Context.Features.Set(user);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, user.Role)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <summary>Respond to the supplied challenge with a JSON problem instead of a redirect.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiProblems.Error(Errors.AuthenticationRequired).ExecuteAsync(Context);

    /// <summary>Respond to the supplied denial with a JSON problem instead of a redirect.</summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblems.Error(Errors.Forbidden).ExecuteAsync(Context);
}
