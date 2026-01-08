using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using System.Runtime.Versioning;

using LoadTrackerWeb.Data;
using LoadTrackerWeb.Models;

namespace LoadTrackerWeb.Controllers;

// AD APIs are Windows-only. You are on Windows Server, so this is fine.
// This attribute silences CA1416 warnings.
[SupportedOSPlatform("windows")]
public sealed class AccountController : Controller
{
    private readonly IOptions<AdSettings> _ad;
    private readonly UserAuthRepository _authRepo;

    public AccountController(IOptions<AdSettings> ad, UserAuthRepository authRepo)
    {
        _ad = ad;
        _authRepo = authRepo;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if (vm.LoginType.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            var result = TryAdLogin(vm.UserName, vm.Password);
            if (!result.Ok)
            {
                vm.Error = "Invalid employee username or password.";
                return View(vm);
            }

            // Employee cookie claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, result.UserName),
                new Claim("DisplayName", result.DisplayName ?? result.UserName),
                new Claim("AuthType", "Employee"),
                new Claim("CanEdit", result.CanEdit ? "true" : "false")
            };

            await SignInAsync(claims);
            return RedirectToAction("PickCustomer", "Loads");
        }
        else
        {
            // Customer DB login
            var (ok, customerCode, canDo) =
                await _authRepo.ValidateCustomerAsync(vm.UserName.Trim(), vm.Password, ct);

            if (!ok || string.IsNullOrWhiteSpace(customerCode))
            {
                vm.Error = "Invalid customer username or password.";
                return View(vm);
            }

            // Customer read-only (your requirement)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, vm.UserName.Trim()),
                new Claim("DisplayName", vm.UserName.Trim()),
                new Claim("AuthType", "Customer"),
                new Claim("CustomerCode", customerCode),
                new Claim("CanEdit", "false")
            };

            await SignInAsync(claims);
            return RedirectToAction("Index", "Loads");
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private (bool Ok, string UserName, string? DisplayName, bool CanEdit) TryAdLogin(string userName, string password)
    {
        try
        {
            // Domain context
            using var ctx = new PrincipalContext(ContextType.Domain, _ad.Value.DomainName);

            // Validate credentials
            if (!ctx.ValidateCredentials(userName, password))
                return (false, userName, null, false);

            var user = UserPrincipal.FindByIdentity(ctx, userName);
            string? display = user?.DisplayName;

            // Check if user is in editor group
            bool canEdit = false;
            var grp = GroupPrincipal.FindByIdentity(ctx, _ad.Value.EditorGroup);

            if (grp != null && user != null && user.IsMemberOf(grp))
                canEdit = true;

            return (true, userName, display, canEdit);
        }
        catch
        {
            return (false, userName, null, false);
        }
    }

    private async Task SignInAsync(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
