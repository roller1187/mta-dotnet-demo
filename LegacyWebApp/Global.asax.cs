using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace LegacyWebApp
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Log application startup
            System.Diagnostics.Trace.WriteLine("Legacy Web Application Started at " + DateTime.Now);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception exception = Server.GetLastError();
            System.Diagnostics.Trace.WriteLine("Application Error: " + exception.Message);
        }

        protected void Session_Start(object sender, EventArgs e)
        {
            // Initialize session with user tracking
            Session["StartTime"] = DateTime.Now;
        }
    }
}
