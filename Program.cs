using CloudWebApp.Data;
using CloudWebApp.Models;
using CloudWebApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Logging.AzureAppServices;
using Serilog.Sinks.GoogleCloudLogging;

//comment change to commit to trigger build

// Configure Serilog
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console();

// Add Google Cloud Logging sink if running on Google Cloud
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE")))
{
    logConfig.WriteTo.GoogleCloudLogging();
}

// Add file logging based on the environment
logConfig.WriteTo.File(GetLogFilePath(), rollingInterval: RollingInterval.Day);

Log.Logger = logConfig.CreateLogger();

try
{
    Log.Information("Starting web application");
    var builder = WebApplication.CreateBuilder(args);

    // Add Azure logging only if running on Azure
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
    {
        builder.Logging.AddAzureWebAppDiagnostics();
    }

    // Add Serilog to the application
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllersWithViews();

    // Add Memory Cache
    builder.Services.AddMemoryCache();

    // Add Authentication
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = true;
            options.Cookie.Name = ".YourApp.Session";
        });

    // Configure the MemoryCacheTicketStore
    builder.Services.AddSingleton<ITicketStore, MemoryCacheTicketStore>();
    builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.SessionStore = options.SessionStore ?? builder.Services.BuildServiceProvider().GetRequiredService<ITicketStore>();
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddIdentity<UserModel, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Add session services
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    builder.Services.Configure<IdentityOptions>(options =>
    {
        // Disable lockout
        options.Lockout.AllowedForNewUsers = false;
        options.Lockout.MaxFailedAccessAttempts = 1000;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.Zero;

        // Disable email confirmation requirement
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;

        // Relax password requirements
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 1;
    });

    // Add HttpClient
    builder.Services.AddHttpClient();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication(); 
    app.UseAuthorization();

    // Use session middleware
    app.UseSession();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string GetLogFilePath()
{
    // Check for Heroku
    var dynoName = Environment.GetEnvironmentVariable("DYNO");
    if (!string.IsNullOrEmpty(dynoName))
    {
        return "app.log";  
    }

    // Check for Google Cloud
    var gaeInstance = Environment.GetEnvironmentVariable("K_SERVICE");
    if (!string.IsNullOrEmpty(gaeInstance))
    {
        return "/tmp/app.log"; 
    }

    // Azure App Service
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
    {
        return "D:\\home\\LogFiles\\Application\\myapp.txt";
    }

    // Local development
    return "logs/myapp.txt";
}
