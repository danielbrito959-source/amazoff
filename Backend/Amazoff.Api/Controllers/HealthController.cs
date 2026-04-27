using Amazoff.Api.Contracts.Health;
using Amazoff.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(AmazoffDbContext dbContext) : ControllerBase
{
    [HttpGet("database")]
    public async Task<ActionResult<DatabaseHealthResponse>> GetDatabaseHealth(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        var databaseName = dbContext.Database.GetDbConnection().Database;

        return Ok(new DatabaseHealthResponse(canConnect, databaseName));
    }
}
