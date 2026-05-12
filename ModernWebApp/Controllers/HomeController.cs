using Microsoft.AspNetCore.Mvc;

namespace ModernWebApp.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<HomeController> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Using IConfiguration (replaces ConfigurationManager)
        ViewBag.AppName = _configuration["AppSettings:AppName"];
        ViewBag.ServerTime = DateTime.Now;

        // Using IWebHostEnvironment (replaces Server.MapPath)
        ViewBag.ServerPath = _environment.ContentRootPath;

        // Session tracking (replaces Global.asax Session_Start)
        if (HttpContext.Session.GetString("StartTime") == null)
        {
            HttpContext.Session.SetString("StartTime", DateTime.Now.ToString());
        }
        ViewBag.SessionStartTime = HttpContext.Session.GetString("StartTime");

        return View();
    }

    public IActionResult About()
    {
        ViewBag.Message = "Modernized ASP.NET Core 8.0 Application";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
