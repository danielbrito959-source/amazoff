using Amazoff.Api.Contracts.Employees;
using Amazoff.Api.Data.Entities;

namespace Amazoff.Api.Features.Employees;

public static class EmployeeMappings
{
    public static EmployeeResponse ToResponse(this User user)
    {
        return new EmployeeResponse(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role?.Name,
            user.ImagePath,
            user.IsActive,
            user.RoleId,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt);
    }
}
