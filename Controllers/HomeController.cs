using CloudWebApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CloudWebApp.Controllers
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
            _logger.LogInformation("Index action called");
            Console.WriteLine("Index action called");
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            _logger.LogInformation("Login action called");
            if (TempData["ErrorMessage"] != null)
            {
                ModelState.AddModelError(string.Empty, TempData["ErrorMessage"].ToString());
            }
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            _logger.LogInformation("Register action called");
            Console.WriteLine("Register action called");
            return View();
        }

        public IActionResult Privacy()
        {
            _logger.LogInformation("Privacy action called");
            Console.WriteLine("Privacy action called");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            _logger.LogError("Error action called");
            Console.WriteLine("Error action called");
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

               public IActionResult About()
        {
            _logger.LogInformation("About action called");
            return View();
        }
    }
}
