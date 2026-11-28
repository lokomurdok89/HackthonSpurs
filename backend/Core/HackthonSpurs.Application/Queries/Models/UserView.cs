namespace HackthonSpurs.Application.Queries;

/// <summary>Expose an account summary without any credential or session material.</summary>
public sealed record UserView(Guid Id, string Username, string DisplayName, string Role, bool Active);
