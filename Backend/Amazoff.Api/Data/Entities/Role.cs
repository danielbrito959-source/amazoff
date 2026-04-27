namespace Amazoff.Api.Data.Entities;

public sealed class Role
{
    public int Id { get; set; }

    public string Name { get; set; } = "user";

    public List<User> Users { get; set; } = [];
}
