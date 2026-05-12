using System;
using System.Web.Mvc;
using LegacyWebApp.Models;
using LegacyWebApp.Services;

namespace LegacyWebApp.Controllers
{
    public class CustomerController : Controller
    {
        private readonly CustomerService _customerService;

        public CustomerController()
        {
            _customerService = new CustomerService();
        }

        public ActionResult List()
        {
            try
            {
                var customers = _customerService.GetAllCustomers();
                return View(customers);
            }
            catch (Exception ex)
            {
                // Using System.Web.HttpContext for logging
                System.Web.HttpContext.Current.Trace.Warn("CustomerController", "Error loading customers", ex);
                ViewBag.Error = "Unable to load customers: " + ex.Message;
                return View();
            }
        }

        public ActionResult Details(int id)
        {
            var customer = _customerService.GetCustomerById(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            return View(customer);
        }

        [HttpPost]
        public ActionResult Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _customerService.AddCustomer(customer);
                    return RedirectToAction("List");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(customer);
        }
    }
}
