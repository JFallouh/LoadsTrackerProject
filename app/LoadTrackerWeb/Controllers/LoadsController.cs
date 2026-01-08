using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

using LoadTrackerWeb.Data;
using LoadTrackerWeb.Hubs;
using LoadTrackerWeb.Models;

namespace LoadTrackerWeb.Controllers;

[Authorize]
public sealed class LoadsController : Controller
{
    private readonly LoadTrackerRepository _repo;
    private readonly IHubContext<LoadTrackerHub> _hub;
    private readonly ILogger<LoadsController> _logger;

    public LoadsController(
        LoadTrackerRepository repo,
        IHubContext<LoadTrackerHub> hub,
        ILogger<LoadsController> logger)
    {
        _repo = repo;
        _hub = hub;
        _logger = logger;
    }

    // Employees land here to pick a customer
    [HttpGet]
    public async Task<IActionResult> PickCustomer(CancellationToken ct)
    {
        LogUserContext("PickCustomer[GET] enter");

        if (!IsEmployee())
        {
            _logger.LogInformation("PickCustomer[GET]: not employee -> redirect to Index.");
            return RedirectToAction("Index");
        }

        try
        {
            var customers = await _repo.GetCustomersAsync(ct);

            _logger.LogInformation(
                "PickCustomer[GET]: GetCustomersAsync returned {Count} customers.",
                customers?.Count ?? 0);

            // OPTIONAL: log first few codes to confirm query returns what you expect
            if (customers != null && customers.Count > 0)
            {
                var preview = string.Join(", ", customers.Take(10).Select(x => x.CustomerCode));
                _logger.LogInformation("PickCustomer[GET]: First customer codes: {Preview}", preview);
            }

            var vm = new CustomerPickViewModel
            {
                Customers = customers
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PickCustomer[GET]: error while loading customer list.");
            // You can show a friendly error page/view if you want
            return StatusCode(500, "Error loading customers. Check logs.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PickCustomer(CustomerPickViewModel vm, CancellationToken ct)
    {
        LogUserContext("PickCustomer[POST] enter");

        if (!IsEmployee())
        {
            _logger.LogInformation("PickCustomer[POST]: not employee -> redirect to Index.");
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(vm.SelectedCustomerCode))
        {
            _logger.LogWarning("PickCustomer[POST]: empty SelectedCustomerCode.");
            ModelState.AddModelError("", "Please select a customer.");
            try
            {
                vm.Customers = await _repo.GetCustomersAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PickCustomer[POST]: error reloading customer list.");
                vm.Customers = new();
            }
            return View(vm);
        }

        var selected = vm.SelectedCustomerCode.Trim();
        _logger.LogInformation("PickCustomer[POST]: selected customer = {CustomerCode}", selected);

        // Update the auth cookie claims (no DB change needed).
        // IMPORTANT: You must sign-out then sign-in to re-issue the cookie with updated claims.
        var claims = User.Claims.ToList();

        claims.RemoveAll(c => c.Type == "SelectedCustomerCode");
        claims.Add(new Claim("SelectedCustomerCode", selected));

        // Re-issue the cookie using the SAME scheme as Program.cs
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        // After re-issuing cookie, log what we THINK we set
        _logger.LogInformation("PickCustomer[POST]: re-issued auth cookie with SelectedCustomerCode={CustomerCode}", selected);

        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        LogUserContext("Index enter");

        // Log incoming year/month (so you see if you’re always hitting current month)
        _logger.LogInformation("Index: query params year={Year} month={Month}", year, month);

        var (customerCode, isEmployee) = ResolveCustomerCode();
        _logger.LogInformation("Index: resolved customerCode='{CustomerCode}', isEmployee={IsEmployee}", customerCode, isEmployee);

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            _logger.LogWarning("Index: customerCode is empty -> redirect to PickCustomer (employee must select a customer).");
            return RedirectToAction("PickCustomer");
        }

        var now = DateTime.Now;
        int y = year ?? now.Year;
        int m = month ?? now.Month;

        var start = new DateTime(y, m, 1);
        var end = start.AddMonths(1);

        _logger.LogInformation("Index: month range start={Start} end={End}", start, end);

        List<LoadRowViewModel> rows;
        try
        {
            rows = await _repo.GetLoadsForMonthAsync(customerCode, start, end, ct);
            _logger.LogInformation("Index: GetLoadsForMonthAsync returned {RowCount} rows for customer {CustomerCode}.",
                rows?.Count ?? 0, customerCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Index: error calling GetLoadsForMonthAsync(customer={CustomerCode}, start={Start}, end={End})",
                customerCode, start, end);
            return StatusCode(500, "Error loading loads. Check logs.");
        }

        int lateOther = rows.Count(r => r.Exception);
        int onTime = rows.Count(r => !r.Exception && r.OnTimeText == "YES");
        int lateCarrier = rows.Count(r => !r.Exception && r.OnTimeText == "NO");

        int total = onTime + lateCarrier + lateOther;

        decimal Pct(int x) => total == 0 ? 0 : Math.Round((decimal)x / total * 100m, 1);

        string customerName = "";
        try
        {
            customerName = await _repo.GetCustomerNameAsync(customerCode, ct);
            _logger.LogInformation("Index: customerName='{CustomerName}'", customerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index: error calling GetCustomerNameAsync(customer={CustomerCode})", customerCode);
            // Not fatal; keep going
        }

        var vm = new LoadsPageViewModel
        {
            IsEmployee = isEmployee,
            CanEdit = CanEdit(),
            CustomerCode = customerCode,
            CustomerName = customerName,
            Year = y,
            Month = m,
            OnTimeCount = onTime,
            LateCarrierCount = lateCarrier,
            LateOtherCount = lateOther,
            OnTimePct = Pct(onTime),
            LateCarrierPct = Pct(lateCarrier),
            LateOtherPct = Pct(lateOther),
            Rows = rows
        };

        return View(vm);
    }

    // Row update (ONLY for editors)
    [Authorize(Policy = "CanEdit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateLoadRequest req, CancellationToken ct)
    {
        LogUserContext("Update enter");

        if (!IsEmployee())
        {
            _logger.LogWarning("Update: non-employee attempted update -> Forbid.");
            return Forbid();
        }

        var (customerCode, _) = ResolveCustomerCode();
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            _logger.LogWarning("Update: no selected customer -> BadRequest.");
            return BadRequest("No customer selected.");
        }

        // Hard length safety to avoid abuse (matches DB column sizes)
        string? delay = req.UserNonCarrierDelay;
        if (delay != null)
        {
            delay = delay.Trim();
            if (delay.Length == 0) delay = null;
            if (delay != null && delay.Length > 255) delay = delay.Substring(0, 255);
        }

        string? comments = req.Comments;
        if (comments != null)
        {
            comments = comments.Trim();
            if (comments.Length == 0) comments = null;
            if (comments != null && comments.Length > 300) comments = comments.Substring(0, 300);
        }

        _logger.LogInformation(
            "Update: customer={CustomerCode} detailLineId={DetailLineId} exception={Exception} delayLen={DelayLen} commentsLen={CommentsLen}",
            customerCode, req.DetailLineId, req.Exception,
            delay?.Length ?? 0, comments?.Length ?? 0);

        bool ok;
        try
        {
            ok = await _repo.UpdateEditableFieldsAsync(
                customerCode,
                req.DetailLineId,
                req.Exception,
                delay,
                comments,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update: exception during UpdateEditableFieldsAsync.");
            return StatusCode(500, "Update failed. Check logs.");
        }

        if (!ok)
        {
            _logger.LogWarning("Update: repo returned ok=false (wrong customer or row not found).");
            return BadRequest("Update failed (wrong customer or row not found).");
        }

        // NOTE: this only notifies current month group; for exact, send year/month from JS
        var yyyymm = $"{DateTime.Now:yyyyMM}";
        await _hub.Clients.Group($"cust:{customerCode}:{yyyymm}")
            .SendAsync("rowUpdated", req.DetailLineId, ct);

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Row(int id, int year, int month, CancellationToken ct)
    {
        LogUserContext("Row enter");

        var (customerCode, _) = ResolveCustomerCode();
        if (string.IsNullOrWhiteSpace(customerCode))
            return Forbid();

        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);

        _logger.LogInformation("Row: id={Id} customer={CustomerCode} start={Start} end={End}", id, customerCode, start, end);

        try
        {
            var rows = await _repo.GetLoadsForMonthAsync(customerCode, start, end, ct);
            var row = rows.FirstOrDefault(r => r.DetailLineId == id);

            if (row == null)
            {
                _logger.LogWarning("Row: not found id={Id} for customer={CustomerCode} in that month.", id, customerCode);
                return NotFound();
            }

            ViewData["CanEdit"] = CanEdit() && IsEmployee();
            return PartialView("_LoadRow", row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Row: error fetching row.");
            return StatusCode(500, "Error fetching row. Check logs.");
        }
    }

    private bool IsEmployee() => User.FindFirstValue("AuthType") == "Employee";
    private bool CanEdit() => User.FindFirstValue("CanEdit") == "true";

    private (string CustomerCode, bool IsEmployee) ResolveCustomerCode()
    {
        if (IsEmployee())
        {
            var sel = User.FindFirstValue("SelectedCustomerCode");
            return (sel ?? "", true);
        }

        var code = User.FindFirstValue("CustomerCode");
        return (code ?? "", false);
    }

    private void LogUserContext(string where)
    {
        // DO NOT log passwords. This only logs identity/claims.
        var name = User?.Identity?.Name ?? "(no name)";
        var authType = User.FindFirstValue("AuthType") ?? "(no AuthType claim)";
        var selected = User.FindFirstValue("SelectedCustomerCode") ?? "(none)";
        var custCode = User.FindFirstValue("CustomerCode") ?? "(none)";
        var canEdit = User.FindFirstValue("CanEdit") ?? "(none)";

        _logger.LogInformation(
            "{Where}: Name={Name} AuthType={AuthType} SelectedCustomerCode={Selected} CustomerCode={CustomerCode} CanEdit={CanEdit}",
            where, name, authType, selected, custCode, canEdit);

        // If you want to see every claim:
        // (Helpful when debugging why customer selection isn’t sticking)
        var claimsDump = string.Join(" | ", User.Claims.Select(c => $"{c.Type}={c.Value}"));
        _logger.LogDebug("{Where}: Claims: {Claims}", where, claimsDump);
    }
}
