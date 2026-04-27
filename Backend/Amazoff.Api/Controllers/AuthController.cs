using Amazoff.Api.Contracts.Auth;
using Amazoff.Api.Data;
using Amazoff.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AmazoffDbContext dbContext) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new LoginResponse(false, "Username obrigatorio.", null));
        }

        var user = await dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(
                currentUser => currentUser.Username == request.Username.Trim(),
                cancellationToken);

        if (user is null)
        {
            return NotFound(new LoginResponse(false, "Utilizador nao encontrado.", null));
        }

        if (!user.IsActive)
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (!PasswordService.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized();
            }

            if (PasswordService.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = PasswordService.HashPassword(request.Password);
            }

            user.LastLoginAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = new LoginUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.RoleId,
            user.FirstName,
            user.LastName);

        return Ok(new LoginResponse(true, "Utilizador encontrado.", response));
    }
}
