using System.Text;
using MetarPulse.Api.Hubs;
using MetarPulse.Api.Services;
using MetarPulse.Infrastructure.Configuration;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// ─── Serilog erken konfigürasyon ───────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("MetarPulse API başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ───────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    // ─── Controller & OpenAPI ──────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // ─── CORS ──────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    // ─── Veritabanı & Repository'ler ──────────────────────────────────────
    builder.Services.AddPostgreSqlDatabase(builder.Configuration);

    // ─── ASP.NET Identity ─────────────────────────────────────────────────
    builder.Services
        .AddIdentity<MetarPulse.Core.Models.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<MetarPulse.Infrastructure.Persistence.PostgreSQL.AppDbContext>();
        // AddDefaultTokenProviders() — .NET 10 package zincirinde mevcut değil.
        // Custom MagicLinkToken ve JwtService kullanıldığı için gerekmiyor.

    // ─── JWT Ayarları ──────────────────────────────────────────────────────
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
        ?? throw new InvalidOperationException("JWT ayarları eksik.");

    builder.Services.AddSingleton(jwtSettings);
    builder.Services.AddScoped<JwtService>();
    builder.Services.AddScoped<MagicLinkService>();

    // ─── JWT Bearer Kimlik Doğrulama ───────────────────────────────────────
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateIssuer   = true,
                ValidIssuer      = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience    = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew        = TimeSpan.FromSeconds(30)
            };

            // SignalR access_token desteği
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });

    // ─── Bellek Cache ──────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();

    // ─── SignalR ───────────────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ─── Weather Provider'lar ──────────────────────────────────────────────
    builder.Services.AddWeatherProviders(builder.Configuration);

    // ─── HttpClient ────────────────────────────────────────────────────────
    builder.Services.AddHttpClient();

    // ─── Arka plan servisleri ──────────────────────────────────────────────
    builder.Services.AddHostedService<MetarPollingService>();

    var app = builder.Build();

    // ─── Pipeline ──────────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // ─── SignalR Hub ───────────────────────────────────────────────────────
    app.MapHub<MetarHub>("/hubs/metar");

    // ─── Sağlık kontrolü ──────────────────────────────────────────────────
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        app = "MetarPulse API",
        version = "1.0.0"
    })).WithName("HealthCheck");

    // ─── EF Core auto-migration (Docker / ilk kurulum) ────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    Log.Information("MetarPulse API hazır. SignalR: /hubs/metar");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MetarPulse API beklenmedik bir hatayla kapandı.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
