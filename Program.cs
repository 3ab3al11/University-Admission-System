using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ANU_Admissions.Data;

// Required by ExcelDataReader on .NET Core/.NET 8 — without it, opening an
// .xlsx stream throws "No data is available for encoding 1252".
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Localization: shared .resx files under /Resources, view + data-annotation
// localization on top of controllers. Culture is stored in a cookie so the
// language switcher just sets the cookie via /Language/Set?culture=...
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
    {
        // Route every DataAnnotation (Required/Display/Compare/RegularExpression/…)
        // through SharedResource so each ViewModel uses the same culture-aware keys
        // instead of duplicating its own .resx file.
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(ANU_Admissions.Resources.SharedResource));
    });

var supportedCultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("ar");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Cookie provider is the only one we want; query/header providers would
    // override the user's saved choice on every request.
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
});

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Official identity provider (mock today, real university API later).
// Swap this single line to point at the real implementation.
builder.Services.AddScoped<ANU_Admissions.Services.IOfficialIdentityProvider,
    ANU_Admissions.Services.MockOfficialIdentityProvider>();

// Admissions gate: combines AdmissionsOpen master switch with the optional
// AdmissionsStartAt / AdmissionsEndAt window. Lives in SystemSettings rows
// only — no schema change.
builder.Services.AddScoped<ANU_Admissions.Services.IAdmissionsGate,
    ANU_Admissions.Services.AdmissionsGateService>();

// Add Identity
builder.Services.AddIdentity<ANU_Admissions.Models.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
{
    // Stronger password policy (applies to new registrations / password changes
    // only; existing accounts are unaffected).
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Account lockout against brute-force (Identity tables already support it).
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders(); // needed for password-reset tokens (no DB change)

// Email sender (development logs the message; replace in production).
builder.Services.AddScoped<ANU_Admissions.Services.IAppEmailSender,
    ANU_Admissions.Services.DevEmailSender>();

// Configure auth cookie (paths + hardening)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    // HTTPS-only cookie in production; SameAsRequest in development so login
    // keeps working over http://localhost.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await ANU_Admissions.Data.DbInitializer.Initialize(services);
}

// Basic security response headers (defense-in-depth). Runs first so it covers
// every response. The CSP is intentionally minimal — it only restricts framing,
// plugins, base URI and form targets, and deliberately omits script-src/style-src
// so existing inline scripts/styles keep working without breaking the UI.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] =
        "frame-ancestors 'self'; object-src 'none'; base-uri 'self'; form-action 'self';";
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Resolve culture (cookie → DefaultRequestCulture) before MVC binds models.
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
