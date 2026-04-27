namespace Amazoff.Api.Contracts.Auth;

public sealed record LoginRequest(string Username, string? Password);
