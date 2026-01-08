using System.ComponentModel.DataAnnotations;

namespace LoadTrackerWeb.Models;

public sealed class UpdateLoadRequest
{
    [Required]
    public int DetailLineId { get; set; }

    // Only these three are editable
    public bool Exception { get; set; }

    // USR_SF_SHORT_DESC (user override)
    public string? UserNonCarrierDelay { get; set; }

    public string? Comments { get; set; }

    // Month currently being viewed (for correct SignalR group notify)
    public int? Year { get; set; }
    public int? Month { get; set; }
}
