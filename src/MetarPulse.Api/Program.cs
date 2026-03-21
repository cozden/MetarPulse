using System.Text;
using FirebaseAdmin;
using Scalar.AspNetCore;
using Google.Apis.Auth.OAuth2;
using MetarPulse.Abstractions.Providers;
using MetarPulse.Api.Auth;
using MetarPulse.Api.Hubs;
using MetarPulse.Api.Services;
using MetarPulse.Infrastructure.Configuration;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using MetarPulse.Infrastructure.Providers.Notam;
using Microsoft.AspNetCore.Authorization;
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

    // ─── Admin Log Buffer (in-memory, Serilog sink) ────────────────────────
    var adminLogBuffer = new AdminLogBuffer();
    builder.Services.AddSingleton(adminLogBuffer);

    // ─── Sistem Ayarları (bakım modu, uptime) ─────────────────────────────
    builder.Services.AddSingleton<SystemSettingsService>();

    // ─── Serilog ───────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console()
        .WriteTo.Sink(new AdminLogBufferSink(adminLogBuffer)));

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

    // ─── Admin Authorization Policy (JWT Admin rolü VEYA X-Admin-Key header) ─
    builder.Services.AddScoped<IAuthorizationHandler, AdminAccessHandler>();
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminAccess", policy =>
            policy.Requirements.Add(new AdminAccessRequirement()));
    });

    // ─── Bellek Cache ──────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();

    // ─── SignalR ───────────────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ─── Weather Provider'lar ──────────────────────────────────────────────
    builder.Services.AddWeatherProviders(builder.Configuration);

    // ─── HttpClient ────────────────────────────────────────────────────────
    builder.Services.AddHttpClient();

    // ─── Firebase Cloud Messaging ──────────────────────────────────────────
    // Service account JSON yolu: appsettings.json → Firebase:ServiceAccountPath
    // Dosya: src/MetarPulse.Api/firebase-service-account.json (gitignore'da)
    var firebaseCredPath = builder.Configuration["Firebase:ServiceAccountPath"];
    if (!string.IsNullOrWhiteSpace(firebaseCredPath) && File.Exists(firebaseCredPath))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(firebaseCredPath)
        });
        Log.Information("Firebase Admin SDK başlatıldı: {Path}", firebaseCredPath);
    }
    else
    {
        Log.Warning("Firebase:ServiceAccountPath yapılandırılmamış veya dosya bulunamadı. FCM push devre dışı.");
    }
    builder.Services.AddSingleton<FcmService>();

    // ─── NOTAM Provider ────────────────────────────────────────────────────
    builder.Services.AddSingleton<INotamProvider, FaaNotamSearchProvider>();

    // ─── Arka plan servisleri ──────────────────────────────────────────────
    builder.Services.AddHostedService<MetarPollingService>();
    builder.Services.AddHostedService<NotamPollingService>();
    builder.Services.AddHostedService<WeatherDataCleanupService>();

    var app = builder.Build();

    // ─── Pipeline ──────────────────────────────────────────────────────────
    // OpenAPI + Scalar UI (her ortamda erişilebilir)
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "MetarPulse API";
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors();

    // ─── Bakım modu — /api/admin/* hariç tüm endpoint'leri 503 döner ──────
    app.Use(async (ctx, next) =>
    {
        var settings = ctx.RequestServices.GetRequiredService<SystemSettingsService>();
        if (settings.MaintenanceMode &&
            !ctx.Request.Path.StartsWithSegments("/api/admin") &&
            !ctx.Request.Path.StartsWithSegments("/health"))
        {
            ctx.Response.StatusCode = 503;
            await ctx.Response.WriteAsJsonAsync(new { message = "Sistem bakım modunda" });
            return;
        }
        await next();
    });
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

        // ─── Admin kullanıcı seed ──────────────────────────────────────────
        var roleManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MetarPulse.Core.Models.ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin"));

        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("User"));

        var seedEmail    = app.Configuration["Admin:SeedEmail"]    ?? "admin@metarpulse.local";
        var seedPassword = app.Configuration["Admin:SeedPassword"] ?? "Admin1234!";

        if (await userManager.FindByEmailAsync(seedEmail) is null)
        {
            var adminUser = new MetarPulse.Core.Models.ApplicationUser
            {
                UserName       = seedEmail,
                Email          = seedEmail,
                EmailConfirmed = true,
                DisplayName    = "Admin"
            };
            var result = await userManager.CreateAsync(adminUser, seedPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Log.Information("Admin kullanıcısı oluşturuldu: {Email}", seedEmail);

            }
            else
            {
                Log.Warning("Admin seed başarısız: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // ─── Mevcut kullanıcılara Admin rolü atama ─────────────────────────
        var promoteEmails  = app.Configuration.GetSection("Admin:PromoteToAdmin").Get<string[]>() ?? [];
        var promotePassword = app.Configuration["Admin:PromoteToAdminPassword"] ?? "Admin1234!";
        foreach (var email in promoteEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                Log.Warning("PromoteToAdmin: kullanıcı bulunamadı: {Email}", email);
                continue;
            }

            // Kilitli hesabı aç
            if (await userManager.IsLockedOutAsync(user))
            {
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1));
                Log.Information("Kilit kaldirildi: {Email}", email);
            }

            // Şifreyi her zaman promotePassword ile sıfırla (var olanın üzerine yaz)
            if (await userManager.HasPasswordAsync(user))
                await userManager.RemovePasswordAsync(user);
            var pwResult = await userManager.AddPasswordAsync(user, promotePassword);
            if (pwResult.Succeeded)
                Log.Information("Şifre sıfırlandı: {Email}", email);
            else
                Log.Warning("Şifre sıfırlanamadı {Email}: {Errors}", email,
                    string.Join(", ", pwResult.Errors.Select(e => e.Description)));

            // Başarısız giriş sayacını sıfırla
            await userManager.ResetAccessFailedCountAsync(user);

            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                await userManager.AddToRoleAsync(user, "Admin");
                Log.Information("Kullanıcı Admin rolüne yükseltildi: {Email}", email);
            }
            else
            {
                Log.Information("Kullanıcı zaten Admin: {Email}", email);
            }
        }
    }

    // ─── DB provider override'larını uygula (WeatherProviderSettings in-memory) ──
    using (var scope = app.Services.CreateScope())
    {
        var db       = scope.ServiceProvider.GetRequiredService<MetarPulse.Infrastructure.Persistence.PostgreSQL.AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MetarPulse.Abstractions.Providers.WeatherProviderSettings>>().Value;
        var overrides = await db.ProviderSettingOverrides.ToListAsync();
        foreach (var o in overrides)
        {
            if (!settings.Providers.TryGetValue(o.ProviderName, out var cfg)) continue;
            if (o.Enabled   != null) cfg.Enabled   = o.Enabled.Value;
            if (o.Priority  != null) cfg.Priority  = o.Priority.Value;
            if (!string.IsNullOrWhiteSpace(o.BaseUrl))  cfg.BaseUrl  = o.BaseUrl;
            if (!string.IsNullOrWhiteSpace(o.ApiKey))   cfg.ApiKey   = o.ApiKey;
            if (o.TimeoutSeconds              != null) cfg.TimeoutSeconds              = o.TimeoutSeconds.Value;
            if (o.RetryCount                  != null) cfg.RetryCount                  = o.RetryCount.Value;
            if (o.CircuitBreakerThreshold     != null) cfg.CircuitBreakerThreshold     = o.CircuitBreakerThreshold.Value;
            if (o.CircuitBreakerDurationSeconds != null) cfg.CircuitBreakerDurationSeconds = o.CircuitBreakerDurationSeconds.Value;
            Log.Information("Provider override uygulandı: {Name}", o.ProviderName);
        }
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
