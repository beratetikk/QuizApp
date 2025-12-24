using Microsoft.EntityFrameworkCore;
using SoruDeneme.Data;
using SoruDeneme.Models;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var appDataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataDir);

var dbFile = Path.Combine(appDataDir, "app.db");
var connStr = $"Data Source={dbFile}";

builder.Services.AddDbContext<SoruDenemeContext>(options =>
    options.UseSqlite(connStr));

builder.Services.AddControllersWithViews();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SoruDenemeContext>();
    db.Database.Migrate();

    static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new AppUser { Username = "egitmen", PasswordHash = HashPassword("egitmen123"), Role = "Egitmen" },
            new AppUser { Username = "ogrenci", PasswordHash = HashPassword("ogrenci123"), Role = "Ogrenci" }
        );
        db.SaveChanges();
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
