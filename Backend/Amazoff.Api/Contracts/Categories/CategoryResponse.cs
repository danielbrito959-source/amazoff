namespace Amazoff.Api.Contracts.Categories;

public sealed record CategoryResponse(
    int Id,
    string Name,
    bool IsActive,
    DateTime DateCreated,
    DateTime DateChanged);
