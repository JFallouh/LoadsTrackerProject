using CsvHelper.Configuration.Attributes;

namespace LoadTrackerExcelImport.Models;

public sealed class LoadTrackerCsvRow
{
    [Name("DETAIL_LINE_ID")] public string? DETAIL_LINE_ID { get; set; }
    [Name("BILL_NUMBER")]    public string? BILL_NUMBER { get; set; }

    [Name("BOL #")]          public string? BOL_NO { get; set; }
    [Name("ORDER #")]        public string? ORDER_NO { get; set; }
    
    [Name("PO #")]          public string? PO_NO { get; set; }

    [Name("DESTINATION")]    public string? DESTINATION { get; set; }
    [Name("DESTNAME")]       public string? DESTNAME { get; set; }
    [Name("DESTCITY")]       public string? DESTCITY { get; set; }
    [Name("DESTPROV")]       public string? DESTPROV { get; set; }

    [Name("CUSTOMER")]       public string? CUSTOMER { get; set; }
    [Name("CALLNAME")]       public string? CALLNAME { get; set; }

    [Name("ORIGIN")]         public string? ORIGIN { get; set; }
    [Name("ORIGNAME")]       public string? ORIGNAME { get; set; }
    [Name("ORIGCITY")]       public string? ORIGCITY { get; set; }
    [Name("ORIGPROV")]       public string? ORIGPROV { get; set; }

    [Name("PICK_UP_BY")]     public string? PICK_UP_BY { get; set; }
    [Name("PICK_UP_BY_END")] public string? PICK_UP_BY_END { get; set; }
    [Name("DELIVER_BY")]     public string? DELIVER_BY { get; set; }
    [Name("DELIVER_BY_END")] public string? DELIVER_BY_END { get; set; }
    [Name("ACTUAL_DELIVERY")] public string? ACTUAL_DELIVERY { get; set; }
    [Name("ACTUAL_PICKUP")] public string? ACTUAL_PICKUP { get; set; }
    [Name("CURRENT_STATUS")] public string? CURRENT_STATUS { get; set; }
    [Name("PALLETS")]        public string? PALLETS { get; set; }
    [Name("CUBE")]           public string? CUBE { get; set; }
    [Name("WEIGHT")]         public string? WEIGHT { get; set; }

    [Name("CUBE_UNITS")]        public string? CUBE_UNITS { get; set; }
    [Name("WEIGHT_UNITS")]      public string? WEIGHT_UNITS { get; set; }
    [Name("TEMPERATURE")]       public string? TEMPERATURE { get; set; }
    [Name("TEMPERATURE_UNITS")] public string? TEMPERATURE_UNITS { get; set; }

    [Name("DANGEROUS_GOODS")]    public string? DANGEROUS_GOODS { get; set; }

    // matches your SQL column typo: REQUESTED_EQUIPMEN
    [Name("REQUESTED_EQUIPMEN")] public string? REQUESTED_EQUIPMEN { get; set; }
    [Name("SF_SHORT_DESC")] public string? SF_SHORT_DESC { get; set; }
}
