namespace Amazoff.Api.Data.Entities;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; }

    public DateTime DateChanged { get; set; }
}
