namespace Amazoff.Api.Features.Auth;

public interface IEmailService
{
    Task SendPasswordRecoveryEmailAsync(string toEmail, string toName, CancellationToken cancellationToken);
}
