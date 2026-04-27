namespace Amazoff.Api.Contracts.Employees;

public sealed record EmployeeUpsertRequest(
    string Username,
    string Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? ImagePath,
    bool IsActive,
    int? RoleId);
