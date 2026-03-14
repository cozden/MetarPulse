using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class AirportConfiguration : IEntityTypeConfiguration<Airport>
{
    public void Configure(EntityTypeBuilder<Airport> builder)
    {
        builder.ToTable("airports");
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.Ident).IsUnique();
        builder.HasIndex(a => a.IsoCountry);
        builder.HasIndex(a => a.Type);
        builder.HasIndex(a => a.IataCode);

        builder.Property(a => a.Ident).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Type).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.IsoCountry).HasMaxLength(5);
        builder.Property(a => a.Municipality).HasMaxLength(100);
        builder.Property(a => a.IataCode).HasMaxLength(5);

        builder.HasMany(a => a.Runways)
               .WithOne(r => r.Airport)
               .HasForeignKey(r => r.AirportIdent)
               .HasPrincipalKey(a => a.Ident)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
