using System.ComponentModel.DataAnnotations;

namespace Amazoff.Api.Contracts.Categories;

public sealed class CategoryUpsertRequest
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
