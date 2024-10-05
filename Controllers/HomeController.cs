using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View("/Views/TusView.cshtml");
    }
}
