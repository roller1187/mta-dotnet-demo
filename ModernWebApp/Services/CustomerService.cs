using Microsoft.Extensions.Caching.Memory;
using ModernWebApp.Models;

namespace ModernWebApp.Services;

public class CustomerService
{
    private static readonly List<Customer> _customers = new();
    private readonly int _maxCustomers;
    private readonly bool _enableCaching;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<CustomerService> logger)
    {
        _cache = cache;
        _logger = logger;

        // Reading from appsettings.json via IConfiguration (replaces ConfigurationManager)
        _maxCustomers = configuration.GetValue<int>("AppSettings:MaxCustomers", 1000);
        _enableCaching = configuration.GetValue<bool>("AppSettings:EnableCaching", true);

        // Initialize with sample data
        if (_customers.Count == 0)
        {
            _customers.Add(new Customer
            {
                Id = 1,
                Name = "John Doe",
                Email = "john@example.com",
                Phone = "555-1234",
                CreatedDate = DateTime.Now.AddDays(-30),
                IsActive = true,
                AccountType = "Premium"
            });
            _customers.Add(new Customer
            {
                Id = 2,
                Name = "Jane Smith",
                Email = "jane@example.com",
                Phone = "555-5678",
                CreatedDate = DateTime.Now.AddDays(-15),
                IsActive = true,
                AccountType = "Standard"
            });
        }
    }

    public List<Customer> GetAllCustomers()
    {
        const string cacheKey = "CustomerList";

        // Using IMemoryCache (replaces HttpContext.Current.Cache)
        if (_enableCaching && _cache.TryGetValue(cacheKey, out List<Customer>? cachedCustomers))
        {
            _logger.LogDebug("Returning customers from cache");
            return cachedCustomers!;
        }

        var customers = _customers.OrderBy(c => c.Name).ToList();

        if (_enableCaching)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, customers, cacheOptions);
            _logger.LogDebug("Cached customer list for 5 minutes");
        }

        return customers;
    }

    public Customer? GetCustomerById(int id)
    {
        return _customers.FirstOrDefault(c => c.Id == id);
    }

    public void AddCustomer(Customer customer)
    {
        if (_customers.Count >= _maxCustomers)
        {
            throw new InvalidOperationException(
                string.Format("Cannot exceed maximum of {0} customers", _maxCustomers));
        }

        customer.Id = _customers.Any() ? _customers.Max(c => c.Id) + 1 : 1;
        customer.CreatedDate = DateTime.Now;
        _customers.Add(customer);

        // Clear cache
        _cache.Remove("CustomerList");
        _logger.LogInformation("Added new customer: {CustomerName} (ID: {CustomerId})",
            customer.Name, customer.Id);
    }
}
