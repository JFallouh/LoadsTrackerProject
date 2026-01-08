// File: Program.cs
using Microsoft.AspNetCore.Authentication.Cookies;
using LoadTrackerWeb.Data;
using LoadTrackerWeb.Hubs;
using LoadTrackerWeb.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Cookie auth (simple, like your old app)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.Cookie.Name = "LoadTrackerWeb.Auth";
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// Authorization: ONLY editors can update
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanEdit", policy =>
        policy.RequireClaim("CanEdit", "true"));
});

builder.Services.Configure<AdSettings>(builder.Configuration.GetSection("ADSettings"));

builder.Services.AddSignalR();

// Repos
builder.Services.AddSingleton<LoadTrackerRepository>();
builder.Services.AddSingleton<UserAuthRepository>();

var app = builder.Build();

// Optional IIS Virtual Directory support (like your /sharedoc)
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<LoadTrackerHub>("/hubs/loadtracker");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Loads}/{action=Index}/{id?}");

app.Run();
