using Amazoff.Api.Contracts.Common;
using Amazoff.Api.Contracts.Employees;
using Amazoff.Api.Data;
using Amazoff.Api.Data.Entities;
using Amazoff.Api.Features.Auth;
using Amazoff.Api.Features.Employees;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("employees")]
public sealed class EmployeesController(AmazoffDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EmployeeResponse>>> GetEmployees(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Username)
            .ToListAsync(cancellationToken);

        var employees = users
            .Select(user => user.ToResponse())
            .ToList();

        return Ok(employees);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> CreateEmployee(
        [FromBody] EmployeeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = EmployeeService.ValidateEmployeeRequest(request, requirePassword: true);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var roleValidationError = await EmployeeService.ValidateRoleAsync(dbContext, request.RoleId, cancellationToken);

        if (roleValidationError is not null)
        {
            return BadRequest(roleValidationError);
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = PasswordService.HashPassword(request.Password!.Trim()),
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            ImagePath = EmployeeService.NullIfWhiteSpace(request.ImagePath),
            IsActive = request.IsActive,
            RoleId = request.RoleId
        };

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EmployeeService.IsDuplicateEntry(ex))
        {
            return Conflict(new ApiMessageResponse("Ja existe um utilizador com esse username ou email."));
        }

        var employee = await EmployeeService.GetEmployeeByIdAsync(dbContext, user.Id, cancellationToken);

        if (employee is null)
        {
            return Problem("Nao foi possivel carregar o trabalhador criado.");
        }

        return Created($"/employees/{employee.Id}", employee);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeResponse>> UpdateEmployee(
        int id,
        [FromBody] EmployeeUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = EmployeeService.ValidateEmployeeRequest(request, requirePassword: false);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var roleValidationError = await EmployeeService.ValidateRoleAsync(dbContext, request.RoleId, cancellationToken);

        if (roleValidationError is not null)
        {
            return BadRequest(roleValidationError);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound(new ApiMessageResponse("Trabalhador nao encontrado."));
        }

        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.FirstName = request.FirstName!.Trim();
        user.LastName = request.LastName!.Trim();
        user.ImagePath = EmployeeService.NullIfWhiteSpace(request.ImagePath);
        user.IsActive = request.IsActive;
        user.RoleId = request.RoleId;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordService.HashPassword(request.Password.Trim());
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EmployeeService.IsDuplicateEntry(ex))
        {
            return Conflict(new ApiMessageResponse("Ja existe um utilizador com esse username ou email."));
        }

        var employee = await EmployeeService.GetEmployeeByIdAsync(dbContext, id, cancellationToken);

        if (employee is null)
        {
            return Problem("Nao foi possivel carregar o trabalhador atualizado.");
        }

        return Ok(employee);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiMessageResponse>> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound(new ApiMessageResponse("Trabalhador nao encontrado."));
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiMessageResponse("Trabalhador removido com sucesso."));
    }
}
