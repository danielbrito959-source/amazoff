using Amazoff.Api.Contracts.Roles;
using Amazoff.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("roles")]
public sealed class RolesController(AmazoffDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoleOptionResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Id)
            .Select(role => new RoleOptionResponse(role.Id, role.Name))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }
}
