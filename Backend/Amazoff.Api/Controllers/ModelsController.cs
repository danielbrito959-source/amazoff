using Amazoff.Api.Contracts.Common;
using Amazoff.Api.Contracts.Models;
using Amazoff.Api.Data;
using Amazoff.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("models")]
public sealed class ModelsController(AmazoffDbContext dbContext) : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    [HttpGet]
    public async Task<ActionResult<List<ModelResponse>>> GetModels(
        [FromQuery] int? categoryId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Models
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(model => model.CategoryId == categoryId.Value);
        }

        var models = await query
            .OrderByDescending(model => model.DateCreated)
            .ThenBy(model => model.Name)
            .Select(model => ToResponse(model))
            .ToListAsync(cancellationToken);

        return Ok(models);
    }

    [HttpPost]
    public async Task<ActionResult<ModelResponse>> CreateModel(
        [FromBody] ModelUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiMessageResponse("O nome do modelo e obrigatorio."));
        }

        var categoryExists = await dbContext.Categories
            .AnyAsync(category => category.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(new ApiMessageResponse("A categoria selecionada nao existe."));
        }

        var model = new Model
        {
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            ImagePath = NullIfWhiteSpace(request.ImagePath),
            IsActive = request.IsActive
        };

        dbContext.Models.Add(model);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/models/{model.Id}", ToResponse(model));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ModelResponse>> UpdateModel(
        int id,
        [FromBody] ModelUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiMessageResponse("O nome do modelo e obrigatorio."));
        }

        var categoryExists = await dbContext.Categories
            .AnyAsync(category => category.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            return BadRequest(new ApiMessageResponse("A categoria selecionada nao existe."));
        }

        var model = await dbContext.Models.FirstOrDefaultAsync(currentModel => currentModel.Id == id, cancellationToken);

        if (model is null)
        {
            return NotFound(new ApiMessageResponse("Modelo nao encontrado."));
        }

        var previousImagePath = model.ImagePath;

        model.Name = request.Name.Trim();
        model.CategoryId = request.CategoryId;
        model.ImagePath = NullIfWhiteSpace(request.ImagePath);
        model.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousImagePath) && string.IsNullOrWhiteSpace(model.ImagePath))
        {
            DeleteStoredModelImage(previousImagePath);
        }

        return Ok(ToResponse(model));
    }

    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<ModelPhotoUploadResponse>> UploadModelPhoto(
        int id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var model = await dbContext.Models.FirstOrDefaultAsync(currentModel => currentModel.Id == id, cancellationToken);

        if (model is null)
        {
            return NotFound(new ApiMessageResponse("Modelo nao encontrado."));
        }

        if (file.Length == 0)
        {
            return BadRequest(new ApiMessageResponse("A imagem enviada esta vazia."));
        }

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedImageExtensions.Contains(extension))
        {
            return BadRequest(new ApiMessageResponse("Formato de imagem invalido. Usa PNG, JPG ou JPEG."));
        }

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "models");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"model-{id}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var previousImagePath = model.ImagePath;

        model.ImagePath = $"/uploads/models/{fileName}";
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousImagePath, model.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            DeleteStoredModelImage(previousImagePath);
        }

        return Ok(new ModelPhotoUploadResponse(model.ImagePath));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiMessageResponse>> DeleteModel(int id, CancellationToken cancellationToken)
    {
        var model = await dbContext.Models.FirstOrDefaultAsync(currentModel => currentModel.Id == id, cancellationToken);

        if (model is null)
        {
            return NotFound(new ApiMessageResponse("Modelo nao encontrado."));
        }

        var previousImagePath = model.ImagePath;

        dbContext.Models.Remove(model);
        await dbContext.SaveChangesAsync(cancellationToken);

        DeleteStoredModelImage(previousImagePath);

        return Ok(new ApiMessageResponse("Modelo eliminado com sucesso."));
    }

    private static ModelResponse ToResponse(Model model)
    {
        return new ModelResponse(
            model.Id,
            model.Name,
            model.CategoryId,
            model.ImagePath,
            model.IsActive,
            model.DateCreated,
            model.DateChanged);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void DeleteStoredModelImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith("/uploads/models/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(imagePath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "models", fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
