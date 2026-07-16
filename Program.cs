using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using ANU_Admissions.Data;
using ANU_Admissions.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Required by ExcelDataReader on .NET Core/.NET 8 — without it, opening an
// .xlsx stream throws "No data is available for encoding 1252".
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Localization: shared .resx files under /Resources, view + data-annotation
// localization on top of controllers. Culture is stored in a cookie so the
// language switcher just sets the cookie via /Language/Set?culture=...
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews(options =>
    {
        // Protect every unsafe MVC request by default. Individual actions keep
        // their explicit attributes for readability, while this global filter
        // also covers any future POST/PUT/PATCH/DELETE action automatically.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
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

// Liveness proves the web process can serve requests. Readiness additionally
// verifies that the configured database is reachable before traffic is sent.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// Official identity provider (mock today, real university API later).
// Swap this single line to point at the real implementation.
builder.Services.AddScoped<ANU_Admissions.Services.IOfficialIdentityProvider,
    ANU_Admissions.Services.MockOfficialIdentityProvider>();

// Admissions gate: combines AdmissionsOpen master switch with the optional
// AdmissionsStartAt / AdmissionsEndAt window. Lives in SystemSettings rows
// only — no schema change.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ANU_Admissions.Services.IAdmissionsGate,
    ANU_Admissions.Services.AdmissionsGateService>();

// Allocation engine: keeps the business workflow out of the MVC controller
// and makes it independently testable.
builder.Services.AddSingleton<ANU_Admissions.Services.IAllocationEngine,
    ANU_Admissions.Services.AllocationEngine>();
builder.Services.AddScoped<ANU_Admissions.Services.IAllocationService,
    ANU_Admissions.Services.AllocationService>();

builder.Services.AddScoped<ANU_Admissions.Services.IOfficialRecordsImportService,
    ANU_Admissions.Services.OfficialRecordsImportService>();
builder.Services.AddSingleton<IOfficialRecordsFileValidator,
    OfficialRecordsFileValidator>();
builder.Services.AddScoped<IOfficialRecordsMaintenanceService,
    OfficialRecordsMaintenanceService>();

builder.Services.AddScoped<ANU_Admissions.Services.IStudentVerificationService,
    ANU_Admissions.Services.StudentVerificationService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

var requireHttpsPublicUrl = !builder.Environment.IsDevelopment();
builder.Services
    .AddOptions<ApplicationUrlOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationUrlOptions.SectionName))
    .Validate(
        options => PublicUrlRules.IsValidBaseUrl(
            options.PublicBaseUrl,
            requireHttpsPublicUrl),
        "ApplicationUrls:PublicBaseUrl must be a trusted absolute origin. " +
        "HTTPS is required outside Development.")
    .ValidateOnStart();
builder.Services.AddSingleton<IPasswordResetLinkFactory, PasswordResetLinkFactory>();

// Public authentication endpoints are partitioned by client IP. Identity's
// account lockout still protects individual accounts, while these policies
// also limit password spraying and reset-email abuse from one client.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>(AuthRateLimitPolicies.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            AuthRateLimitPolicies.GetClientPartitionKey(context),
            _ => AuthRateLimitPolicies.CreateLoginLimiterOptions()));

    options.AddPolicy<string>(AuthRateLimitPolicies.ForgotPassword, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            AuthRateLimitPolicies.GetClientPartitionKey(context),
            _ => AuthRateLimitPolicies.CreateForgotPasswordLimiterOptions()));

    options.AddPolicy<string>(AuthRateLimitPolicies.ResetPassword, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            AuthRateLimitPolicies.GetClientPartitionKey(context),
            _ => AuthRateLimitPolicies.CreateResetPasswordLimiterOptions()));

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        var message = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? "محاولات كثيرة جدًا. حاول مرة أخرى بعد قليل."
            : "Too many attempts. Please try again later.";
        await context.HttpContext.Response.WriteAsync(message, cancellationToken);
    };
});

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

// Reset links may appear in logs only during local development. Other
// environments use a safe no-content fallback until a real provider is wired.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IAppEmailSender, DevEmailSender>();
}
else
{
    builder.Services.AddScoped<IAppEmailSender, DisabledEmailSender>();
}

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

// Give every response a server-generated request ID that also appears in the
// structured logging scope. It can be quoted when diagnosing a problem without
// trusting a client-controlled header value.
var requestLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("ANU_Admissions.RequestTracing");

app.Use(async (context, next) =>
{
    var requestId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    context.TraceIdentifier = requestId;
    context.Response.Headers["X-Request-ID"] = requestId;

    using (requestLogger.BeginScope(new Dictionary<string, object>
    {
        ["RequestId"] = requestId
    }))
    {
        await next();
    }
});

// Security response headers (defense-in-depth). A fresh nonce is generated for
// every request so only Razor scripts/styles emitted by this app can run inline;
// event-handler attributes are blocked entirely.
app.Use(async (context, next) =>
{
    var nonce = ContentSecurityPolicy.CreateNonce();
    context.Items[ContentSecurityPolicy.NonceItemKey] = nonce;

    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] =
            ContentSecurityPolicy.BuildHeader(
                nonce,
                requireHttps: !app.Environment.IsDevelopment());

        // Sensitive authenticated pages must not remain in browser or proxy
        // caches after logout or on a shared machine.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            headers.CacheControl = "no-store, no-cache, max-age=0";
            headers.Pragma = "no-cache";
            headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";
        }

        return Task.CompletedTask;
    });

    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Render a friendly localized page for otherwise-empty 4xx/5xx responses while
// preserving the original status code for clients and monitoring tools.
app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Resolve culture (cookie → DefaultRequestCulture) before MVC binds models.
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = OperationalHealthResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = OperationalHealthResponseWriter.WriteAsync
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Exposes the top-level entry point to WebApplicationFactory integration tests.
public partial class Program { }
