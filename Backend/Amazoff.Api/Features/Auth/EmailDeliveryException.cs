namespace Amazoff.Api.Features.Auth;

public sealed class EmailDeliveryException(string message) : Exception(message);
