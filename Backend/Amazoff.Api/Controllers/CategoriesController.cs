using Amazoff.Api.Contracts.Categories;
using Amazoff.Api.Contracts.Common;
using Amazoff.Api.Data;
using Amazoff.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("categories")]
public sealed class CategoriesController(AmazoffDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderByDescending(category => category.DateCreated)
            .ThenBy(category => category.Name)
            .Select(category => ToResponse(category))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        [FromBody] CategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiMessageResponse("O nome da categoria é obrigatório."));
        }

        var category = new Category
        {
            Name = request.Name.Trim(),
            IsActive = request.IsActive
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/categories/{category.Id}", ToResponse(category));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(
        int id,
        [FromBody] CategoryUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiMessageResponse("O nome da categoria é obrigatório."));
        }

        var category = await dbContext.Categories.FirstOrDefaultAsync(currentCategory => currentCategory.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound(new ApiMessageResponse("Categoria não encontrada."));
        }

        category.Name = request.Name.Trim();
        category.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(category));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiMessageResponse>> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(currentCategory => currentCategory.Id == id, cancellationToken);

        if (category is null)
        {
            return NotFound(new ApiMessageResponse("Categoria não encontrada."));
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiMessageResponse("Categoria eliminada com sucesso."));
    }

    private static CategoryResponse ToResponse(Category category)
    {
        return new CategoryResponse(
            category.Id,
            category.Name,
            category.IsActive,
            category.DateCreated,
            category.DateChanged);
    }
}
