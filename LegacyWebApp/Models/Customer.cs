using System;
using System.ComponentModel.DataAnnotations;

namespace LegacyWebApp.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string Phone { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; }

        public string AccountType { get; set; }
    }
}
