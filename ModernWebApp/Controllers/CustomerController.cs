using Microsoft.AspNetCore.Mvc;
using ModernWebApp.Models;
using ModernWebApp.Services;

namespace ModernWebApp.Controllers;

public class CustomerController : Controller
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;

    // Using constructor injection (replaces manual instantiation)
    public CustomerController(
        CustomerService customerService,
        ILogger<CustomerController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    public IActionResult List()
    {
        try
        {
            var customers = _customerService.GetAllCustomers();
            return View(customers);
        }
        catch (Exception ex)
        {
            // Using ILogger (replaces HttpContext.Current.Trace)
            _logger.LogError(ex, "Error loading customers");
            ViewBag.Error = string.Format("Unable to load customers: {0}", ex.Message);
            return View();
        }
    }

    public IActionResult Details(int id)
    {
        var customer = _customerService.GetCustomerById(id);
        if (customer == null)
        {
            return NotFound();
        }
        return View(customer);
    }

    [HttpPost]
    public IActionResult Create(Customer customer)
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
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError("", ex.Message);
            }
        }
        return View(customer);
    }
}
