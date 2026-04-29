using Amazoff.Api.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Amazoff.Api.Features.Auth;

public sealed class BrevoEmailService(
    HttpClient httpClient,
    IOptions<BrevoOptions> brevoOptions) : IEmailService
{
    private readonly BrevoOptions options = brevoOptions.Value;

    public async Task SendPasswordRecoveryEmailAsync(string toEmail, string toName, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", options.ApiKey);

        var payload = new
        {
            sender = new
            {
                email = options.SenderEmail,
                name = options.SenderName
            },
            to = new[]
            {
                new
                {
                    email = toEmail,
                    name = toName
                }
            },
            subject = "Recuperacao de password Amazoff",
            textContent = BuildBody(toName)
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey)
            || string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            throw new InvalidOperationException("Brevo settings are not fully configured.");
        }
    }

    private static string BuildBody(string toName)
    {
        var greeting = string.IsNullOrWhiteSpace(toName) ? "Ola" : $"Ola {toName}";

        return
            $"{greeting},{Environment.NewLine}{Environment.NewLine}" +
            "Recebemos um pedido para recuperar a sua password da conta Amazoff." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Por favor entre em contacto com a administracao para concluir a recuperacao da password." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Amazoff";
    }
}
