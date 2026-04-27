using Amazoff.Api.Contracts.Common;
using Amazoff.Api.Contracts.Employees;
using Amazoff.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Features.Employees;

public static class EmployeeService
{
    public static ApiMessageResponse? ValidateEmployeeRequest(EmployeeUpsertRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return new ApiMessageResponse("Username obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new ApiMessageResponse("Email obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return new ApiMessageResponse("Primeiro nome obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return new ApiMessageResponse("Ultimo nome obrigatorio.");
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
        {
            return new ApiMessageResponse("Password obrigatoria.");
        }

        if (!request.RoleId.HasValue)
        {
            return new ApiMessageResponse("Role obrigatoria.");
        }

        return null;
    }

    public static async Task<ApiMessageResponse?> ValidateRoleAsync(
        AmazoffDbContext dbContext,
        int? roleId,
        CancellationToken cancellationToken)
    {
        if (!roleId.HasValue)
        {
            return new ApiMessageResponse("Role obrigatoria.");
        }

        var roleExists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(role => role.Id == roleId.Value, cancellationToken);

        return roleExists
            ? null
            : new ApiMessageResponse("Role invalida.");
    }

    public static async Task<EmployeeResponse?> GetEmployeeByIdAsync(
        AmazoffDbContext dbContext,
        int id,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(currentUser => currentUser.Role)
            .FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        return user?.ToResponse();
    }

    public static bool IsDuplicateEntry(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
