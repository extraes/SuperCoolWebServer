using Microsoft.AspNetCore.Mvc;

namespace SuperCoolWebServer.Controllers;

[Route("/")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View("/Views/TusView.cshtml");
    }
}
