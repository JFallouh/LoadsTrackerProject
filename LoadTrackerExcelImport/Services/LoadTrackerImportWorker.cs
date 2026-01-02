using Cronos;

namespace LoadTrackerExcelImport.Services;

public sealed class LoadTrackerImportWorker : BackgroundService
{
    private readonly ILogger<LoadTrackerImportWorker> _log;
    private readonly LoadTrackerImporter _importer;
    private readonly CronExpression _cron;
    private readonly TimeZoneInfo _tz;

    public LoadTrackerImportWorker(ILogger<LoadTrackerImportWorker> log, IConfiguration cfg, LoadTrackerImporter importer)
    {
        _log = log;
        _importer = importer;

        _cron = CronExpression.Parse(cfg["Importer:Cron"] ?? "*/15 * * * *");
        _tz = TimeZoneInfo.FindSystemTimeZoneById(cfg["Importer:TimeZoneId"] ?? "Eastern Standard Time");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _tz);
            _log.LogInformation("Tick {NowLocal}", nowLocal);

            await _importer.ImportOnceAsync(stoppingToken);

            var nextUtc = _cron.GetNextOccurrence(nowUtc, _tz);
            if (!nextUtc.HasValue)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var nextLocal = TimeZoneInfo.ConvertTimeFromUtc(nextUtc.Value, _tz);
            var delay = nextLocal - nowLocal;
            if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);

            _log.LogInformation("Next run {NextLocal} (delay {Delay})", nextLocal, delay);
            await Task.Delay(delay, stoppingToken);
        }
    }
}
