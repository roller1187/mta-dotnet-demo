using System.ComponentModel.DataAnnotations;

namespace ModernWebApp.Models;

public class Customer
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    public DateTime CreatedDate { get; set; }

    public bool IsActive { get; set; }

    public string? AccountType { get; set; }
}
