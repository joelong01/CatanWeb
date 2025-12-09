using System.Diagnostics;
using Catan3.GameService.Models;
using Microsoft.AspNetCore.Mvc;

namespace Catan3.GameService.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Return service info for root requests
            return Ok(new
            {
                service = "Catan3 Game Service",
                status = "running",
                signalRHub = "/gameHub",
                api = new
                {
                    newGame = "POST /api/game/new",
                    health = "GET /health"
                }
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
