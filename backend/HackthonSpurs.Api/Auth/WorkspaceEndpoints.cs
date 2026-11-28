using System.Security.Cryptography;
using System.Text.Json;
using HackthonSpurs.Resources;

namespace HackthonSpurs.Api.Auth;

internal static class WorkspaceEndpoints
{
    /// <summary>Map session and admin operations on the supplied authentication-protected application.</summary>
    public static void Map(WebApplication app)
    {
        var session = app.MapGroup(Wire.Auth);
        session.MapPost(Wire.Login, Login).AllowAnonymous().RequireRateLimiting(Wire.Login);
        session.MapGet(Wire.Me, Me);
        session.MapPost(Wire.Logout, Logout);

        var users = app.MapGroup(Wire.Users).RequireAuthorization(Wire.AdminRole);
        users.MapGet("", List);
        users.MapPost("", Create);
        users.MapPatch(Wire.ById, Update);
    }

    /// <summary>Issue a session after verifying the credentials in this HTTP exchange.</summary>
    /// <param name="context">Current HTTP request and response.</param>
    /// <param name="sessions">Session service for credential verification and cookie issuance.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<IResult> Login(HttpContext context, WorkspaceSessions sessions, CancellationToken ct)
    {
        var input = await ReadAsync<LoginRequest>(context.Request, ct);
        if (input is null) return ApiProblems.Error(Errors.InvalidAuthRequest);
        var user = await sessions.SignInAsync(context, input, ct);
        return user is null ? ApiProblems.Error(Errors.InvalidCredentials) : Results.Ok(user);
    }

    /// <summary>Return the public user summary already loaded for this authenticated exchange.</summary>
    private static IResult Me(HttpContext context) => Results.Ok(context.Features.Get<UserView>());

    /// <summary>Revoke the session associated with this exchange and clear its cookie.</summary>
    /// <param name="context">Current HTTP request and response.</param>
    /// <param name="sessions">Session service used to revoke the cookie.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<IResult> Logout(HttpContext context, WorkspaceSessions sessions, CancellationToken ct)
    {
        await sessions.SignOutAsync(context, ct);
        return Results.NoContent();
    }

    /// <summary>List public account summaries using the supplied service and request cancellation.</summary>
    private static async Task<IResult> List(WorkspaceUsers users, CancellationToken ct) =>
        Results.Ok(await users.ListAsync(ct));

    /// <summary>Create an account from the validated body of the acting administrator's request.</summary>
    /// <param name="request">HTTP request containing the administrator cookie and new account fields.</param>
    /// <param name="users">Account management service.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<IResult> Create(HttpRequest request, WorkspaceUsers users, CancellationToken ct)
    {
        var input = await ReadAsync<CreateUserRequest>(request, ct);
        if (input is null) return ApiProblems.Error(Errors.InvalidAuthRequest);
        var change = await users.CreateAsync(SessionTokens.Read(request), input, ct);
        return change.Error is { } error ? ApiProblems.Error(error)
            : Results.Created($"{Wire.Users}/{change.User!.Id:D}", change.User);
    }

    /// <summary>Apply changes to the requested account and clear the cookie if the caller reset their own password.</summary>
    /// <param name="id">Account UUID from the route.</param>
    /// <param name="context">HTTP exchange containing the current session cookie and changed fields.</param>
    /// <param name="users">Account management service.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<IResult> Update(string id, HttpContext context, WorkspaceUsers users, CancellationToken ct)
    {
        if (!Guid.TryParseExact(id, "D", out var key)) return ApiProblems.Error(Errors.InvalidAuthRequest);
        var input = await ReadAsync<UpdateUserRequest>(context.Request, ct);
        if (input is null) return ApiProblems.Error(Errors.InvalidAuthRequest);
        var change = await users.UpdateAsync(SessionTokens.Read(context.Request), key, input, ct);
        if (change.Error is { } error) return ApiProblems.Error(error);
        if (change.CallerRevoked) SessionTokens.Clear(context);
        return Results.Ok(change.User);
    }

    /// <summary>Read a bounded UTF-8 JSON body without returning raw parser exceptions or retaining body bytes.</summary>
    /// <param name="request">Untrusted HTTP request containing an account or credential payload.</param>
    /// <param name="ct">Request cancellation.</param>
    private static async Task<T?> ReadAsync<T>(HttpRequest request, CancellationToken ct) where T : class
    {
        if (!request.HasJsonContentType() || request.ContentLength > Policy.MaxAuthBody) return null;
        var buffer = new byte[Policy.MaxAuthBody + 1];
        try
        {
            int size = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, ct);
            return size > Policy.MaxAuthBody ? null : JsonSerializer.Deserialize<T>(buffer.AsSpan(0, size), JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
