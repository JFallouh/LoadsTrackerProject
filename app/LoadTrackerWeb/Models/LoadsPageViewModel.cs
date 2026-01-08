namespace LoadTrackerWeb.Models;

public sealed class LoadsPageViewModel
{
    public bool IsEmployee { get; set; }
    public bool CanEdit { get; set; }

    public string CustomerCode { get; set; } = "";
    public string? CustomerName { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    // Summary box numbers
    public int OnTimeCount { get; set; }
    public int LateCarrierCount { get; set; }
    public int LateOtherCount { get; set; }
    public int TotalCount => OnTimeCount + LateCarrierCount + LateOtherCount;

    public decimal OnTimePct { get; set; }
    public decimal LateCarrierPct { get; set; }
    public decimal LateOtherPct { get; set; }

    public List<LoadRowViewModel> Rows { get; set; } = new();
}
