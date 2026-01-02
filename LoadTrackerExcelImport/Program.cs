using LoadTrackerExcelImport.Services;
using Serilog;
using Microsoft.Extensions.Hosting.WindowsServices;


var builder = Host.CreateApplicationBuilder(args);

// Run as Windows Service
builder.Services.AddWindowsService();

// Serilog rolling file logs beside the exe
var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.File(
        Path.Combine(logDir, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// DI
builder.Services.AddSingleton<LoadTrackerImporter>();
builder.Services.AddHostedService<LoadTrackerImportWorker>();

var host = builder.Build();
host.Run();
