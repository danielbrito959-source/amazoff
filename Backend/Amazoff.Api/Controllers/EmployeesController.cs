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
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

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

    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<EmployeePhotoUploadResponse>> UploadEmployeePhoto(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound(new ApiMessageResponse("Trabalhador nao encontrado."));
        }

        if (file.Length == 0)
        {
            return BadRequest(new ApiMessageResponse("A imagem enviada esta vazia."));
        }

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new ApiMessageResponse("Formato de imagem invalido. Usa PNG, JPG ou JPEG."));
        }

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"user-{id}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var previousImagePath = user.ImagePath;

        user.ImagePath = $"/uploads/users/{fileName}";
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousImagePath, user.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteStoredUserImage(previousImagePath);
        }

        return Ok(new EmployeePhotoUploadResponse(user.ImagePath));
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

        var previousImagePath = user.ImagePath;

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

        if (!string.IsNullOrWhiteSpace(previousImagePath) && string.IsNullOrWhiteSpace(user.ImagePath))
        {
            DeleteStoredUserImage(previousImagePath);
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

        user.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiMessageResponse("Trabalhador desativado com sucesso."));
    }

    [HttpPost("{id:int}/activate")]
    public async Task<ActionResult<ApiMessageResponse>> ActivateEmployee(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(currentUser => currentUser.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound(new ApiMessageResponse("Trabalhador nao encontrado."));
        }

        user.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiMessageResponse("Trabalhador incluído com sucesso."));
    }
    private static void DeleteStoredUserImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith("/uploads/users/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(imagePath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users", fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
