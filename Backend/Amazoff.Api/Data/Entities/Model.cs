namespace Amazoff.Api.Data.Entities;

public sealed class Model
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; }

    public DateTime DateChanged { get; set; }

    public Category? Category { get; set; }
}
