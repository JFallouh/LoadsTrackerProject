using System.Data;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LoadTrackerExcelImport.Models;
using Microsoft.Data.SqlClient;

namespace LoadTrackerExcelImport.Services;

public sealed class LoadTrackerImporter
{
    private readonly ILogger<LoadTrackerImporter> _log;
    private readonly IConfiguration _cfg;

    private static readonly SemaphoreSlim _runLock = new(1, 1);
    private static DateTime? _lastImportedWriteUtc;

    public LoadTrackerImporter(ILogger<LoadTrackerImporter> log, IConfiguration cfg)
    {
        _log = log;
        _cfg = cfg;
    }

    public async Task ImportOnceAsync(CancellationToken ct)
    {
        // Prevent overlapping runs inside the same process
        if (!await _runLock.WaitAsync(0, ct))
        {
            _log.LogInformation("Skipped: previous run still executing.");
            return;
        }

        try
        {
            var csvPath = _cfg["Importer:CsvPath"];
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                _log.LogError("Importer:CsvPath missing.");
                return;
            }

            if (!File.Exists(csvPath))
            {
                _log.LogWarning("CSV not found: {Path}", csvPath);
                return;
            }

            var writeUtc = File.GetLastWriteTimeUtc(csvPath);
            var skipIfUnchanged = bool.TryParse(_cfg["Importer:SkipIfFileUnchanged"], out var b) && b;

            if (skipIfUnchanged && _lastImportedWriteUtc.HasValue && _lastImportedWriteUtc.Value == writeUtc)
            {
                _log.LogInformation("CSV unchanged ({WriteUtc}). Skipping.", writeUtc);
                return;
            }

            // Copy to temp first (avoids partial reads while another process writes)
            var temp = Path.Combine(Path.GetTempPath(), $"JF_LIVE_SHEET_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.csv");
            await CopyFileReadShareAsync(csvPath, temp, ct);

            var data = ReadCsvToDataTable(temp);
            if (data.Rows.Count == 0)
            {
                _log.LogInformation("No rows parsed from CSV.");
                return;
            }

            var connStr = _cfg.GetConnectionString("LoadTracker");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _log.LogError("ConnectionStrings:LoadTracker missing.");
                return;
            }

            var retries = int.TryParse(_cfg["Importer:MaxRetries"], out var r) ? Math.Max(1, r) : 3;

            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    var (updated, inserted) = await UpsertAsync(connStr, data, ct);
                    _log.LogInformation("Done. Rows={Rows} Updated={Updated} Inserted={Inserted}", data.Rows.Count, updated, inserted);
                    _lastImportedWriteUtc = writeUtc;
                    break;
                }
                catch (SqlException ex) when (ex.Number == 1205 && attempt < retries) // deadlock
                {
                    _log.LogWarning(ex, "Deadlock attempt {Attempt}/{Retries}. Retrying...", attempt, retries);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
                }
            }

            try { File.Delete(temp); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportOnce failed.");
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static async Task CopyFileReadShareAsync(string srcPath, string destPath, CancellationToken ct)
    {
        await using var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, ct);
    }

    private DataTable ReadCsvToDataTable(string csvPath)
    {
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => (args.Header ?? "").Trim()
        };

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, cfg);
        var records = csv.GetRecords<LoadTrackerCsvRow>().ToList();

        // Deduplicate by DETAIL_LINE_ID (keep last)
        var byId = new Dictionary<int, LoadTrackerCsvRow>();
        foreach (var rec in records)
        {
            if (!TryInt(rec.DETAIL_LINE_ID, out var id)) continue;
            byId[id] = rec;
        }

        var table = CreateSchema();
        foreach (var (id, r) in byId)
        {
            var row = table.NewRow();
            row["DETAIL_LINE_ID"] = id;

            row["BILL_NUMBER"] = DbStr(r.BILL_NUMBER);
            row["BOL #"] = DbStr(r.BOL_NO);
            row["ORDER #"] = DbStr(r.ORDER_NO);

            row["DESTINATION"] = DbStr(r.DESTINATION);
            row["DESTNAME"] = DbStr(r.DESTNAME);
            row["DESTCITY"] = DbStr(r.DESTCITY);
            row["DESTPROV"] = DbStr(r.DESTPROV);

            row["CUSTOMER"] = DbStr(r.CUSTOMER);
            row["CALLNAME"] = DbStr(r.CALLNAME);

            row["ORIGIN"] = DbStr(r.ORIGIN);
            row["ORIGNAME"] = DbStr(r.ORIGNAME);
            row["ORIGCITY"] = DbStr(r.ORIGCITY);
            row["ORIGPROV"] = DbStr(r.ORIGPROV);

            row["PICK_UP_BY"] = DbDt(r.PICK_UP_BY);
            row["PICK_UP_BY_END"] = DbDt(r.PICK_UP_BY_END);
            row["DELIVER_BY"] = DbDt(r.DELIVER_BY);
            row["DELIVER_BY_END"] = DbDt(r.DELIVER_BY_END);

            row["CURRENT_STATUS"] = DbStr(r.CURRENT_STATUS);
            row["PALLETS"] = DbDbl(r.PALLETS);
            row["CUBE"] = DbDbl(r.CUBE);
            row["WEIGHT"] = DbDbl(r.WEIGHT);

            row["CUBE_UNITS"] = DbStr(r.CUBE_UNITS);
            row["WEIGHT_UNITS"] = DbStr(r.WEIGHT_UNITS);
            row["TEMPERATURE"] = DbDbl(r.TEMPERATURE);
            row["TEMPERATURE_UNITS"] = DbStr(r.TEMPERATURE_UNITS);

            row["DANGEROUS_GOODS"] = DbStr(r.DANGEROUS_GOODS);
            row["REQUESTED_EQUIPMEN"] = DbStr(r.REQUESTED_EQUIPMEN);

            table.Rows.Add(row);
        }

        return table;
    }

    private static DataTable CreateSchema()
    {
        var t = new DataTable();

        t.Columns.Add("DETAIL_LINE_ID", typeof(int));
        t.Columns.Add("BILL_NUMBER", typeof(string));
        t.Columns.Add("BOL #", typeof(string));
        t.Columns.Add("ORDER #", typeof(string));

        t.Columns.Add("DESTINATION", typeof(string));
        t.Columns.Add("DESTNAME", typeof(string));
        t.Columns.Add("DESTCITY", typeof(string));
        t.Columns.Add("DESTPROV", typeof(string));

        t.Columns.Add("CUSTOMER", typeof(string));
        t.Columns.Add("CALLNAME", typeof(string));

        t.Columns.Add("ORIGIN", typeof(string));
        t.Columns.Add("ORIGNAME", typeof(string));
        t.Columns.Add("ORIGCITY", typeof(string));
        t.Columns.Add("ORIGPROV", typeof(string));

        t.Columns.Add("PICK_UP_BY", typeof(DateTime));
        t.Columns.Add("PICK_UP_BY_END", typeof(DateTime));
        t.Columns.Add("DELIVER_BY", typeof(DateTime));
        t.Columns.Add("DELIVER_BY_END", typeof(DateTime));

        t.Columns.Add("CURRENT_STATUS", typeof(string));
        t.Columns.Add("PALLETS", typeof(double));
        t.Columns.Add("CUBE", typeof(double));
        t.Columns.Add("WEIGHT", typeof(double));

        t.Columns.Add("CUBE_UNITS", typeof(string));
        t.Columns.Add("WEIGHT_UNITS", typeof(string));
        t.Columns.Add("TEMPERATURE", typeof(double));
        t.Columns.Add("TEMPERATURE_UNITS", typeof(string));

        t.Columns.Add("DANGEROUS_GOODS", typeof(string));
        t.Columns.Add("REQUESTED_EQUIPMEN", typeof(string));

        return t;
    }

    private async Task<(int updated, int inserted)> UpsertAsync(string connStr, DataTable data, CancellationToken ct)
    {
        var timeout = int.TryParse(_cfg["Importer:CommandTimeoutSeconds"], out var s) ? s : 120;

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        await using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        // Prevent multiple instances (different servers/services) from importing at the same time
        if (!await TryAcquireAppLockAsync(conn, tx, timeout, ct))
        {
            _log.LogInformation("Skipped: app lock not acquired (another instance importing).");
            await tx.RollbackAsync(ct);
            return (0, 0);
        }

        // Create temp staging table
        var createSql = @"
CREATE TABLE #staging (
    DETAIL_LINE_ID        INT            NOT NULL,
    BILL_NUMBER           VARCHAR(20)     NULL,
    [BOL #]               VARCHAR(40)     NULL,
    [ORDER #]             VARCHAR(40)     NULL,

    DESTINATION           VARCHAR(10)     NULL,
    DESTNAME              VARCHAR(40)     NULL,
    DESTCITY              VARCHAR(30)     NULL,
    DESTPROV              VARCHAR(4)      NULL,

    CUSTOMER              VARCHAR(10)     NULL,
    CALLNAME              VARCHAR(40)     NULL,

    ORIGIN                VARCHAR(10)     NULL,
    ORIGNAME              VARCHAR(40)     NULL,
    ORIGCITY              VARCHAR(30)     NULL,
    ORIGPROV              VARCHAR(4)      NULL,

    PICK_UP_BY            DATETIME       NULL,
    PICK_UP_BY_END        DATETIME       NULL,
    DELIVER_BY            DATETIME       NULL,
    DELIVER_BY_END        DATETIME       NULL,

    CURRENT_STATUS        VARCHAR(10)     NULL,
    PALLETS               FLOAT          NULL,
    CUBE                  FLOAT          NULL,
    WEIGHT                FLOAT          NULL,

    CUBE_UNITS            VARCHAR(3)      NULL,
    WEIGHT_UNITS          VARCHAR(3)      NULL,
    TEMPERATURE           FLOAT          NULL,
    TEMPERATURE_UNITS     VARCHAR(5)      NULL,

    DANGEROUS_GOODS       CHAR(5)         NULL,
    REQUESTED_EQUIPMEN    VARCHAR(20)     NULL
);";

        await using (var cmd = new SqlCommand(createSql, conn, tx) { CommandTimeout = timeout })
            await cmd.ExecuteNonQueryAsync(ct);

        // Bulk insert into #staging
        using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, tx))
        {
            bulk.DestinationTableName = "#staging";
            bulk.BulkCopyTimeout = timeout;

            foreach (DataColumn c in data.Columns)
                bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);

            await bulk.WriteToServerAsync(data, ct);
        }

        // Update ONLY when something changed (does NOT touch future manual columns like Notes)
        var updateSql = @"
UPDATE t
SET
    t.BILL_NUMBER        = s.BILL_NUMBER,
    t.[BOL #]            = s.[BOL #],
    t.[ORDER #]          = s.[ORDER #],
    t.DESTINATION        = s.DESTINATION,
    t.DESTNAME           = s.DESTNAME,
    t.DESTCITY           = s.DESTCITY,
    t.DESTPROV           = s.DESTPROV,
    t.CUSTOMER           = s.CUSTOMER,
    t.CALLNAME           = s.CALLNAME,
    t.ORIGIN             = s.ORIGIN,
    t.ORIGNAME           = s.ORIGNAME,
    t.ORIGCITY           = s.ORIGCITY,
    t.ORIGPROV           = s.ORIGPROV,
    t.PICK_UP_BY         = s.PICK_UP_BY,
    t.PICK_UP_BY_END     = s.PICK_UP_BY_END,
    t.DELIVER_BY         = s.DELIVER_BY,
    t.DELIVER_BY_END     = s.DELIVER_BY_END,
    t.CURRENT_STATUS     = s.CURRENT_STATUS,
    t.PALLETS            = s.PALLETS,
    t.CUBE               = s.CUBE,
    t.WEIGHT             = s.WEIGHT,
    t.CUBE_UNITS         = s.CUBE_UNITS,
    t.WEIGHT_UNITS       = s.WEIGHT_UNITS,
    t.TEMPERATURE        = s.TEMPERATURE,
    t.TEMPERATURE_UNITS  = s.TEMPERATURE_UNITS,
    t.DANGEROUS_GOODS    = s.DANGEROUS_GOODS,
    t.REQUESTED_EQUIPMEN = s.REQUESTED_EQUIPMEN
FROM dbo.LoadTracker t
JOIN #staging s ON s.DETAIL_LINE_ID = t.DETAIL_LINE_ID
WHERE
    ISNULL(t.BILL_NUMBER,'') <> ISNULL(s.BILL_NUMBER,'')
 OR ISNULL(t.[BOL #],'') <> ISNULL(s.[BOL #],'')
 OR ISNULL(t.[ORDER #],'') <> ISNULL(s.[ORDER #],'')
 OR ISNULL(t.DESTINATION,'') <> ISNULL(s.DESTINATION,'')
 OR ISNULL(t.DESTNAME,'') <> ISNULL(s.DESTNAME,'')
 OR ISNULL(t.DESTCITY,'') <> ISNULL(s.DESTCITY,'')
 OR ISNULL(t.DESTPROV,'') <> ISNULL(s.DESTPROV,'')
 OR ISNULL(t.CUSTOMER,'') <> ISNULL(s.CUSTOMER,'')
 OR ISNULL(t.CALLNAME,'') <> ISNULL(s.CALLNAME,'')
 OR ISNULL(t.ORIGIN,'') <> ISNULL(s.ORIGIN,'')
 OR ISNULL(t.ORIGNAME,'') <> ISNULL(s.ORIGNAME,'')
 OR ISNULL(t.ORIGCITY,'') <> ISNULL(s.ORIGCITY,'')
 OR ISNULL(t.ORIGPROV,'') <> ISNULL(s.ORIGPROV,'')
 OR ISNULL(t.PICK_UP_BY,'19000101') <> ISNULL(s.PICK_UP_BY,'19000101')
 OR ISNULL(t.PICK_UP_BY_END,'19000101') <> ISNULL(s.PICK_UP_BY_END,'19000101')
 OR ISNULL(t.DELIVER_BY,'19000101') <> ISNULL(s.DELIVER_BY,'19000101')
 OR ISNULL(t.DELIVER_BY_END,'19000101') <> ISNULL(s.DELIVER_BY_END,'19000101')
 OR ISNULL(t.CURRENT_STATUS,'') <> ISNULL(s.CURRENT_STATUS,'')
 OR ISNULL(t.PALLETS,0) <> ISNULL(s.PALLETS,0)
 OR ISNULL(t.CUBE,0) <> ISNULL(s.CUBE,0)
 OR ISNULL(t.WEIGHT,0) <> ISNULL(s.WEIGHT,0)
 OR ISNULL(t.CUBE_UNITS,'') <> ISNULL(s.CUBE_UNITS,'')
 OR ISNULL(t.WEIGHT_UNITS,'') <> ISNULL(s.WEIGHT_UNITS,'')
 OR ISNULL(t.TEMPERATURE,0) <> ISNULL(s.TEMPERATURE,0)
 OR ISNULL(t.TEMPERATURE_UNITS,'') <> ISNULL(s.TEMPERATURE_UNITS,'')
 OR ISNULL(t.DANGEROUS_GOODS,'') <> ISNULL(s.DANGEROUS_GOODS,'')
 OR ISNULL(t.REQUESTED_EQUIPMEN,'') <> ISNULL(s.REQUESTED_EQUIPMEN,'');";

        int updated;
        await using (var cmd = new SqlCommand(updateSql, conn, tx) { CommandTimeout = timeout })
            updated = await cmd.ExecuteNonQueryAsync(ct);

        // Insert new rows only
        var insertSql = @"
INSERT INTO dbo.LoadTracker (
    DETAIL_LINE_ID, BILL_NUMBER, [BOL #], [ORDER #],
    DESTINATION, DESTNAME, DESTCITY, DESTPROV,
    CUSTOMER, CALLNAME,
    ORIGIN, ORIGNAME, ORIGCITY, ORIGPROV,
    PICK_UP_BY, PICK_UP_BY_END, DELIVER_BY, DELIVER_BY_END,
    CURRENT_STATUS, PALLETS, CUBE, WEIGHT,
    CUBE_UNITS, WEIGHT_UNITS, TEMPERATURE, TEMPERATURE_UNITS,
    DANGEROUS_GOODS, REQUESTED_EQUIPMEN
)
SELECT
    s.DETAIL_LINE_ID, s.BILL_NUMBER, s.[BOL #], s.[ORDER #],
    s.DESTINATION, s.DESTNAME, s.DESTCITY, s.DESTPROV,
    s.CUSTOMER, s.CALLNAME,
    s.ORIGIN, s.ORIGNAME, s.ORIGCITY, s.ORIGPROV,
    s.PICK_UP_BY, s.PICK_UP_BY_END, s.DELIVER_BY, s.DELIVER_BY_END,
    s.CURRENT_STATUS, s.PALLETS, s.CUBE, s.WEIGHT,
    s.CUBE_UNITS, s.WEIGHT_UNITS, s.TEMPERATURE, s.TEMPERATURE_UNITS,
    s.DANGEROUS_GOODS, s.REQUESTED_EQUIPMEN
FROM #staging s
LEFT JOIN dbo.LoadTracker t ON t.DETAIL_LINE_ID = s.DETAIL_LINE_ID
WHERE t.DETAIL_LINE_ID IS NULL;";

        int inserted;
        await using (var cmd = new SqlCommand(insertSql, conn, tx) { CommandTimeout = timeout })
            inserted = await cmd.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return (updated, inserted);
    }

    private static async Task<bool> TryAcquireAppLockAsync(SqlConnection conn, SqlTransaction tx, int timeoutSeconds, CancellationToken ct)
    {
        using var cmd = new SqlCommand("EXEC @res = sp_getapplock @Resource, @LockMode, @LockOwner, @LockTimeout;", conn, tx);
        cmd.Parameters.AddWithValue("@Resource", "LoadTrackerCsvImport");
        cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
        cmd.Parameters.AddWithValue("@LockOwner", "Transaction");
        cmd.Parameters.AddWithValue("@LockTimeout", 0); // don't wait, just skip if locked

        var resParam = new SqlParameter("@res", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(resParam);

        cmd.CommandTimeout = timeoutSeconds;
        await cmd.ExecuteNonQueryAsync(ct);

        var code = (int)(resParam.Value ?? -999);
        return code >= 0; // >=0 means lock acquired
    }

    private static object DbStr(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    private static object DbDt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
        if (DateTime.TryParse(s.Trim(), new CultureInfo("en-US"), DateTimeStyles.AssumeLocal, out var dt)) return dt;
        if (DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt)) return dt;
        return DBNull.Value;
    }

    private static object DbDbl(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
        if (double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        if (double.TryParse(s.Trim(), NumberStyles.Any, new CultureInfo("en-US"), out d)) return d;
        return DBNull.Value;
    }

    private static bool TryInt(string? s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
