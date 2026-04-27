namespace Amazoff.Api.Contracts.Employees;

public sealed record EmployeeResponse(
    int Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? RoleName,
    string? ImagePath,
    bool IsActive,
    int? RoleId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);
