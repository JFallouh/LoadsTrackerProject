namespace LoadTrackerWeb.Models;

public sealed class CustomerPickViewModel
{
    public string? SelectedCustomerCode { get; set; }
    public List<(string CustomerCode, string CallName)> Customers { get; set; } = new();
}
