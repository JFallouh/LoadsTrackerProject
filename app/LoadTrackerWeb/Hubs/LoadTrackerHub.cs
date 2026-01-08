using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LoadTrackerWeb.Hubs;

[Authorize]
public sealed class LoadTrackerHub : Hub
{
    // Client calls: joinGroup(customerCode, "202601")
    public async Task JoinGroup(string customerCode, string yyyymm)
    {
        // Safety: user can only join groups they are allowed to see
        var authType = Context.User?.FindFirst("AuthType")?.Value;

        if (authType == "Customer")
        {
            var myCode = Context.User?.FindFirst("CustomerCode")?.Value;
            if (!string.Equals(myCode, customerCode, StringComparison.OrdinalIgnoreCase))
                throw new HubException("Not allowed.");
        }

        if (authType == "Employee")
        {
            var sel = Context.User?.FindFirst("SelectedCustomerCode")?.Value;
            if (!string.Equals(sel, customerCode, StringComparison.OrdinalIgnoreCase))
                throw new HubException("Not allowed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"cust:{customerCode}:{yyyymm}");
    }
}
