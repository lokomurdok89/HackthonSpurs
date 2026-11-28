using System.Security.Cryptography;
using System.Text;
using HackthonSpurs.Resources;
using Microsoft.AspNetCore.WebUtilities;

namespace HackthonSpurs.Api.Auth.Sessions;

internal static class SessionTokens
{
    /// <summary>Generate an opaque cookie token with 256 bits of cryptographic randomness.</summary>
    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(Policy.SessionTokenBytes));

    /// <summary>Return the lookup digest for the supplied HTTP request's well-formed cookie.</summary>
    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Wire.WorkspaceCookie, out var token) &&
        token.Length == Policy.SessionTokenLength &&
        token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? Digest(token) : null;

    /// <summary>Derive the stable server-side digest of the supplied opaque token.</summary>
    public static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));

    /// <summary>Set the supplied response's new opaque session token and fixed expiry.</summary>
    public static void Issue(HttpContext context, string token, DateTimeOffset expiresAt)
    {
        var options = Options(context.Request);
        options.Expires = expiresAt;
        context.Response.Cookies.Append(Wire.WorkspaceCookie, token, options);
    }

    /// <summary>Clear the workspace cookie in the supplied HTTP response.</summary>
    public static void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Wire.WorkspaceCookie, Options(context.Request));

    /// <summary>Apply same-origin HTTP settings, using Secure for an HTTPS request.</summary>
    private static CookieOptions Options(HttpRequest request) => new()
    {
        Path = "/", HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = request.IsHttps, IsEssential = true
    };
}
