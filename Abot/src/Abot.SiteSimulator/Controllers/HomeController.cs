using Microsoft.AspNetCore.Mvc;

namespace Abot.SiteSimulator.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
