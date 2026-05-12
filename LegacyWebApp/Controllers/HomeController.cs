using System;
using System.Configuration;
using System.Web.Mvc;

namespace LegacyWebApp.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            // Using ConfigurationManager - needs migration
            ViewBag.AppName = ConfigurationManager.AppSettings["AppName"];
            ViewBag.ServerTime = DateTime.Now;

            // Using Server object - System.Web specific
            ViewBag.ServerPath = Server.MapPath("~/");

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Legacy ASP.NET MVC 5 Application";
            return View();
        }
    }
}
