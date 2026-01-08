using System.ComponentModel.DataAnnotations;

namespace LoadTrackerWeb.Models;

public sealed class LoginViewModel
{
    [Required]
    public string LoginType { get; set; } = "Employee"; // "Employee" or "Customer"

    [Required]
    public string UserName { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";

    public string? Error { get; set; }
}
