namespace HackthonSpurs.Api.Contracts;

internal sealed record LoginRequest(string? Username, string? Password);
internal sealed record CreateUserRequest(string? Username, string? DisplayName, string? Password, string? Role);
internal sealed record UpdateUserRequest(string? DisplayName, string? Role, bool? Active, string? Password);
