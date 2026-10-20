using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ServiceDesk.WebApi.IntegrationTests.Authentication;

[ApiController]
[Authorize]
[Route("test/authentication-probe")]
public sealed class AuthenticationProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}
