namespace Amazoff.Api.Contracts.Auth;

public sealed record LoginUserResponse(
    int Id,
    string Username,
    string Email,
    int? RoleId,
    string? RoleName,
    string FirstName,
    string LastName);
