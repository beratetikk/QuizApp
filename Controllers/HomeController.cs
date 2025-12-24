using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SoruDeneme.Models;
using SoruDeneme.Filters;

namespace SoruDeneme.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Login");
        }

        [RequireRole("Egitmen")]
        public IActionResult EgitmenHome()
        {
            return View();
        }

        [RequireRole("Ogrenci")]
        public IActionResult OgrenciHome()
        {
            return View();
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
