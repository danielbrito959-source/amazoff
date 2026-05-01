using System.ComponentModel.DataAnnotations;

namespace Amazoff.Api.Contracts.Models;

public sealed class ModelUpsertRequest
{
    [Required(ErrorMessage = "O nome e obrigatorio.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A categoria e obrigatoria.")]
    public int CategoryId { get; set; }

    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;
}
