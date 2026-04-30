using Amazoff.Api.Contracts.Auth;
using Amazoff.Api.Data;
using Amazoff.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AmazoffDbContext dbContext, IEmailService emailService) : ControllerBase
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
            .Include(currentUser => currentUser.Role)
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
            user.Role?.Name,
            user.FirstName,
            user.LastName);

        return Ok(new LoginResponse(true, "Utilizador encontrado.", response));
    }

    [HttpPost("recover-password")]
    public async Task<ActionResult<PasswordRecoveryLookupResponse>> RecoverPassword(
        [FromBody] PasswordRecoveryLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new PasswordRecoveryLookupResponse(false, "Email obrigatorio."));
        }

        var email = request.Email.Trim();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(currentUser => currentUser.Email == email, cancellationToken);

        if (user is null)
        {
            return NotFound(new PasswordRecoveryLookupResponse(false, "Nao existe utilizador com esse email."));
        }

        try
        {
            await emailService.SendPasswordRecoveryEmailAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new PasswordRecoveryLookupResponse(false, "O servico Brevo nao esta configurado."));
        }
        catch (EmailDeliveryException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new PasswordRecoveryLookupResponse(false, exception.Message));
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new PasswordRecoveryLookupResponse(false, "Não foi possível comunicar com a Brevo."));
        }

        return Ok(new PasswordRecoveryLookupResponse(true, "Consulte o seu email para recuperar a passowrd."));
    }
}
