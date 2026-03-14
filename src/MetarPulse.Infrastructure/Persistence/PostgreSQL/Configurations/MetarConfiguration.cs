using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class MetarConfiguration : IEntityTypeConfiguration<Metar>
{
    public void Configure(EntityTypeBuilder<Metar> builder)
    {
        builder.ToTable("metar_history");
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.StationId);
        builder.HasIndex(m => m.ObservationTime);
        builder.HasIndex(m => new { m.StationId, m.ObservationTime });

        builder.Property(m => m.StationId).HasMaxLength(10).IsRequired();
        builder.Property(m => m.RawText).HasMaxLength(500).IsRequired();
        builder.Property(m => m.SourceProvider).HasMaxLength(30);
        builder.Property(m => m.Trend).HasMaxLength(100);
        builder.Property(m => m.RvrRaw).HasMaxLength(100);

        // CloudLayers ve WeatherConditions JSON olarak saklanır
        builder.Property(m => m.CloudLayers)
               .HasColumnType("jsonb");
        builder.Property(m => m.WeatherConditions)
               .HasColumnType("jsonb");
    }
}
