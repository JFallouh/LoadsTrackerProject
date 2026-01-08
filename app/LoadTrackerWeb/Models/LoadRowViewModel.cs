namespace LoadTrackerWeb.Models;

public sealed class LoadRowViewModel
{
    public int DetailLineId { get; set; }

    public string? Probill { get; set; }     // BILL_NUMBER
    public string? BolNo { get; set; }       // [BOL #]
    public string? OrderNo { get; set; }     // [ORDER #]
    public string? PoNo { get; set; }        // [PO #]

    public string? Receiver { get; set; }    // DESTNAME
    public string? ReceiverCity { get; set; }// DESTCITY
    public string? ReceiverProv { get; set; }// DESTPROV

    public DateTime? ActualPickup { get; set; } // ACTUAL_PICKUP

    public DateTime? DeliverBy { get; set; }    // DELIVER_BY
    public DateTime? DeliverByEnd { get; set; } // DELIVER_BY_END

    public string? RadText { get; set; }     // friendly range string

    public string? CurrentStatus { get; set; } // CURRENT_STATUS

    public DateTime? ActualDelivery { get; set; } // ACTUAL_DELIVERY
    public string? DeliveryDateText { get; set; } // derived
    public string? DeliveryTimeText { get; set; } // derived

    public bool Exception { get; set; } // EXCEPTION

    public string? OnTimeText { get; set; } // "YES" / "NO" / ""

    public string? NonCarrierDelay { get; set; } // COALESCE(USR_SF_SHORT_DESC, SF_SHORT_DESC)
    public string? UserNonCarrierDelay { get; set; } // USR_SF_SHORT_DESC (editable source)

    public string? Comments { get; set; } // COMMENTS
}
