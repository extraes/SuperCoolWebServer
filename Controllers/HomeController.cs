using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View("/Views/IndexView.cshtml");
    }

    [Authorize]
    [Route("dashboard")]
    public IActionResult Dashboard()
    {
        return View("/Views/DashboardView.cshtml");
    }
}