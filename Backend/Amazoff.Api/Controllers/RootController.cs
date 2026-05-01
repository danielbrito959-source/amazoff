using Microsoft.AspNetCore.Mvc;

namespace Amazoff.Api.Controllers;

[ApiController]
[Route("")]
public sealed class RootController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            name = "Amazoff API",
            status = "online",
            endpoints = new[]
            {
                "/health/database",
                "/auth/login",
                "/categories",
                "/employees",
                "/models",
                "/roles"
            }
        });
    }
}
