using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using LegacyWebApp.Models;

namespace LegacyWebApp.Services
{
    public class CustomerService
    {
        private static List<Customer> _customers = new List<Customer>();
        private readonly int _maxCustomers;

        public CustomerService()
        {
            // Reading from Web.config - needs migration to appsettings.json
            _maxCustomers = int.Parse(ConfigurationManager.AppSettings["MaxCustomers"]);

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
            // Using HttpContext - needs refactoring for .NET 8
            var cacheEnabled = bool.Parse(ConfigurationManager.AppSettings["EnableCaching"]);

            if (cacheEnabled && HttpContext.Current.Cache["CustomerList"] != null)
            {
                return (List<Customer>)HttpContext.Current.Cache["CustomerList"];
            }

            var customers = _customers.OrderBy(c => c.Name).ToList();

            if (cacheEnabled)
            {
                HttpContext.Current.Cache.Insert("CustomerList", customers, null,
                    DateTime.Now.AddMinutes(5), System.Web.Caching.Cache.NoSlidingExpiration);
            }

            return customers;
        }

        public Customer GetCustomerById(int id)
        {
            return _customers.FirstOrDefault(c => c.Id == id);
        }

        public void AddCustomer(Customer customer)
        {
            if (_customers.Count >= _maxCustomers)
            {
                throw new InvalidOperationException(string.Format("Cannot exceed maximum of {0} customers", _maxCustomers));
            }

            customer.Id = _customers.Any() ? _customers.Max(c => c.Id) + 1 : 1;
            customer.CreatedDate = DateTime.Now;
            _customers.Add(customer);

            // Clear cache
            HttpContext.Current.Cache.Remove("CustomerList");
        }
    }
}
