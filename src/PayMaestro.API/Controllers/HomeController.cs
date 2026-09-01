using Microsoft.AspNetCore.Mvc;

namespace PayMaestro.API.Controllers;

/// <summary>Sends a browser that opens the root straight to the Swagger UI.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index() => Redirect("/swagger");
}
