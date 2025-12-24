using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoruDeneme.Data;
using System.Security.Cryptography;
using System.Text;

namespace SoruDeneme.Controllers
{
    public class LoginController : Controller
    {
        private readonly SoruDenemeContext _context;

        public LoginController(SoruDenemeContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role == "Egitmen") return RedirectToAction("EgitmenHome", "Home");
            if (role == "Ogrenci") return RedirectToAction("OgrenciHome", "Home");

            return View();
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string userType, string username, string password)
        {
            var passHash = HashPassword(password);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == passHash && u.Role == userType);

            if (user == null)
            {
                ViewBag.Error = "Kullanıcı adı / şifre / rol hatalı!";
                return View("Index");
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("Username", user.Username);

            if (user.Role == "Egitmen")
                return RedirectToAction("EgitmenHome", "Home");

            return RedirectToAction("OgrenciHome", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
