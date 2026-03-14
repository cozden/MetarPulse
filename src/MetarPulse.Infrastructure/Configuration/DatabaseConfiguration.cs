using MetarPulse.Abstractions.Providers;
using MetarPulse.Abstractions.Repositories;
using MetarPulse.Infrastructure.AirportDb;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using MetarPulse.Infrastructure.Persistence.PostgreSQL.Repositories;
using MetarPulse.Infrastructure.Persistence.SQLite;
using MetarPulse.Infrastructure.Providers.AirportData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MetarPulse.Infrastructure.Configuration;

public static class DatabaseConfiguration
{
    /// <summary>
    /// PostgreSQL DbContext + Repository pattern DI kaydı.
    /// Identity kaydı API katmanında (Program.cs) yapılır.
    /// </summary>
    public static IServiceCollection AddPostgreSqlDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string bulunamadı.");

        var dataSource = new NpgsqlDataSourceBuilder(connectionString)
            .EnableDynamicJson()
            .Build();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            }));

        // Repository pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMetarRepository, MetarRepository>();
        services.AddScoped<IAirportRepository, AirportRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();

        // Airport provider (OurAirports)
        services.AddScoped<IAirportDataProvider, OurAirportsProvider>();

        // Aylık meydan sync + startup seed (arka plan servisi)
        services.AddHostedService<AirportSyncService>();

        return services;
    }

    /// <summary>
    /// SQLite LocalDbContext DI kaydı.
    /// MAUI client tarafından kullanılır.
    /// </summary>
    public static IServiceCollection AddSqliteLocalDatabase(
        this IServiceCollection services,
        string? dbPath = null)
    {
        services.AddDbContext<LocalDbContext>(options =>
        {
            var path = dbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "metarpulse_local.db");
            options.UseSqlite($"Data Source={path}");
        });

        return services;
    }
}
