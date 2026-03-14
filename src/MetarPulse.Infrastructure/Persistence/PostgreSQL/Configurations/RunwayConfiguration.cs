using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class RunwayConfiguration : IEntityTypeConfiguration<Runway>
{
    public void Configure(EntityTypeBuilder<Runway> builder)
    {
        builder.ToTable("runways");
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.AirportIdent);

        builder.Property(r => r.AirportIdent).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Surface).HasMaxLength(50);
        builder.Property(r => r.LeIdent).HasMaxLength(5);
        builder.Property(r => r.HeIdent).HasMaxLength(5);
    }
}
