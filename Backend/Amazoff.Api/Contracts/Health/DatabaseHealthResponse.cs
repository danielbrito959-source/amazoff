namespace Amazoff.Api.Contracts.Health;

public sealed record DatabaseHealthResponse(bool Connected, string? Database);
