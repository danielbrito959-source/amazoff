namespace Amazoff.Api.Contracts.Auth;

public sealed record LoginResponse(bool Success, string Message, LoginUserResponse? User);
