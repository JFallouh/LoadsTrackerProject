using Dapper;
using Microsoft.Data.SqlClient;
using LoadTrackerWeb.Models;

namespace LoadTrackerWeb.Data;

public sealed class LoadTrackerRepository
{
    private readonly IConfiguration _cfg;
    public LoadTrackerRepository(IConfiguration cfg) => _cfg = cfg;

    private SqlConnection OpenLoadTracker() =>
        new SqlConnection(_cfg.GetConnectionString("LoadTrackerDb"));

    public async Task<List<(string CustomerCode, string CallName)>> GetCustomersAsync(CancellationToken ct)
    {
        const string sql = @"
SELECT DISTINCT
    LTRIM(RTRIM([CUSTOMER])) AS CustomerCode,
    MAX(LTRIM(RTRIM([CALLNAME]))) AS CallName
FROM dbo.LoadTracker
WHERE [CUSTOMER] IS NOT NULL AND LTRIM(RTRIM([CUSTOMER])) <> ''
GROUP BY LTRIM(RTRIM([CUSTOMER]))
ORDER BY MAX(LTRIM(RTRIM([CALLNAME])));
";
        await using var con = OpenLoadTracker();
        var rows = await con.QueryAsync<(string CustomerCode, string CallName)>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<string?> GetCustomerNameAsync(string customerCode, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP 1 [CALLNAME]
FROM dbo.LoadTracker
WHERE [CUSTOMER] = @CustomerCode
ORDER BY [CALLNAME];
";
        await using var con = OpenLoadTracker();
        return await con.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(sql, new { CustomerCode = customerCode }, cancellationToken: ct));
    }

    public async Task<List<LoadRowViewModel>> GetLoadsForMonthAsync(
        string customerCode, DateTime start, DateTime end, CancellationToken ct)
    {
        // We filter by DELIVER_BY month window.
        // If you want a different rule (pickup month etc.), change it here.
        const string sql = @"
SELECT
    [DETAIL_LINE_ID] AS DetailLineId,
    [BILL_NUMBER]    AS Probill,
    [BOL #]          AS BolNo,
    [ORDER #]        AS OrderNo,
    [PO #]           AS PoNo,
    [DESTNAME]       AS Receiver,
    [DESTCITY]       AS ReceiverCity,
    [DESTPROV]       AS ReceiverProv,
    [ACTUAL_PICKUP]  AS ActualPickup,
    [DELIVER_BY]     AS DeliverBy,
    [DELIVER_BY_END] AS DeliverByEnd,
    [CURRENT_STATUS] AS CurrentStatus,
    [ACTUAL_DELIVERY] AS ActualDelivery,
    [EXCEPTION]      AS Exception,
    [SF_SHORT_DESC]  AS SfShortDesc,
    [USR_SF_SHORT_DESC] AS UsrSfShortDesc,
    [COMMENTS]       AS Comments
FROM dbo.LoadTracker
WHERE [CUSTOMER] = @CustomerCode
  AND [DELIVER_BY] >= @StartUtcOrLocal
  AND [DELIVER_BY] <  @EndUtcOrLocal
ORDER BY [DELIVER_BY], [DETAIL_LINE_ID];
";

        await using var con = OpenLoadTracker();

        var rows = await con.QueryAsync(
            new CommandDefinition(sql,
                new { CustomerCode = customerCode, StartUtcOrLocal = start, EndUtcOrLocal = end },
                cancellationToken: ct));

        // Build view models + computed columns safely in code
        var list = new List<LoadRowViewModel>();

        foreach (var r in rows)
        {
            // Dapper returned a dynamic row
            int id = r.DetailLineId;
            DateTime? deliverBy = r.DeliverBy;
            DateTime? deliverByEnd = r.DeliverByEnd;
            DateTime? actualDelivery = r.ActualDelivery;

            string radText = BuildRadText(deliverBy, deliverByEnd);
            string onTime = ComputeOnTime(actualDelivery, deliverBy, deliverByEnd);

            string? usrDelay = r.UsrSfShortDesc;
            string? sfDelay = r.SfShortDesc;
            string? effectiveDelay = !string.IsNullOrWhiteSpace(usrDelay) ? usrDelay : sfDelay;

            string? deliveryDateText = actualDelivery?.ToString("yyyy-MM-dd");
            string? deliveryTimeText = actualDelivery?.ToString("HH:mm");

            list.Add(new LoadRowViewModel
            {
                DetailLineId = id,
                Probill = r.Probill,
                BolNo = r.BolNo,
                OrderNo = r.OrderNo,
                PoNo = r.PoNo,
                Receiver = r.Receiver,
                ReceiverCity = r.ReceiverCity,
                ReceiverProv = r.ReceiverProv,
                ActualPickup = r.ActualPickup,
                DeliverBy = deliverBy,
                DeliverByEnd = deliverByEnd,
                RadText = radText,
                CurrentStatus = r.CurrentStatus,
                ActualDelivery = actualDelivery,
                DeliveryDateText = deliveryDateText,
                DeliveryTimeText = deliveryTimeText,
                Exception = r.Exception,
                OnTimeText = onTime,
                NonCarrierDelay = effectiveDelay,
                UserNonCarrierDelay = usrDelay,
                Comments = r.Comments
            });
        }

        return list;
    }

    public async Task<bool> UpdateEditableFieldsAsync(
        string customerCode,
        int detailLineId,
        bool exception,
        string? userDelay,
        string? comments,
        CancellationToken ct)
    {
        // Hard safety rules:
        // - Update ONLY the three allowed columns
        // - Also include CUSTOMER in WHERE (prevents cross-customer updates)
        // - Parameterized SQL (no injection)
        const string sql = @"
UPDATE dbo.LoadTracker
SET
    [EXCEPTION] = @Exception,
    [USR_SF_SHORT_DESC] = @UsrSfShortDesc,
    [COMMENTS] = @Comments
WHERE [DETAIL_LINE_ID] = @DetailLineId
  AND [CUSTOMER] = @CustomerCode;
";

        // Small normalization to avoid crazy whitespace
        userDelay = string.IsNullOrWhiteSpace(userDelay) ? null : userDelay.Trim();
        comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();

        await using var con = OpenLoadTracker();
        int affected = await con.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                Exception = exception,
                UsrSfShortDesc = userDelay,
                Comments = comments,
                DetailLineId = detailLineId,
                CustomerCode = customerCode
            },
            cancellationToken: ct));

        return affected == 1;
    }

    private static string BuildRadText(DateTime? start, DateTime? end)
    {
        if (start is null && end is null) return "";

        if (start is not null && end is not null)
        {
            // Same day => "YYYY-MM-DD HH:mm–HH:mm"
            if (start.Value.Date == end.Value.Date)
                return $"{start:yyyy-MM-dd HH:mm}–{end:HH:mm}";

            // Different day => "YYYY-MM-DD HH:mm – YYYY-MM-dd HH:mm"
            return $"{start:yyyy-MM-dd HH:mm} – {end:yyyy-MM-dd HH:mm}";
        }

        // Only one side exists
        return start is not null ? start.Value.ToString("yyyy-MM-dd HH:mm") : end!.Value.ToString("yyyy-MM-dd HH:mm");
    }

    private static string ComputeOnTime(DateTime? actual, DateTime? windowStart, DateTime? windowEnd)
    {
        // If no actual delivery, show blank (not "NO")
        if (actual is null) return "";

        // If range exists, check within range
        if (windowStart is not null && windowEnd is not null)
        {
            return (actual.Value >= windowStart.Value && actual.Value <= windowEnd.Value) ? "YES" : "NO";
        }

        // If only DeliverBy exists, check <= DeliverBy
        if (windowStart is not null && windowEnd is null)
        {
            return (actual.Value <= windowStart.Value) ? "YES" : "NO";
        }

        return "";
    }
}
