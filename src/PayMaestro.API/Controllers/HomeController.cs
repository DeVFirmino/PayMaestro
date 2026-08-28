using Microsoft.AspNetCore.Mvc;

namespace PayMaestro.API.Controllers;

[ApiController]
[Route("")]
public sealed class HomeController : ControllerBase
{
    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Index() => Redirect("/swagger");
}
