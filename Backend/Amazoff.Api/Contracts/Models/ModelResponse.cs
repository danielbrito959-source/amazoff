namespace Amazoff.Api.Contracts.Models;

public sealed record ModelResponse(
    int Id,
    string Name,
    int CategoryId,
    string? ImagePath,
    bool IsActive,
    DateTime DateCreated,
    DateTime DateChanged);
