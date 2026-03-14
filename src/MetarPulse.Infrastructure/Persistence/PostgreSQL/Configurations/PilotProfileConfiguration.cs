using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class PilotProfileConfiguration : IEntityTypeConfiguration<PilotProfile>
{
    public void Configure(EntityTypeBuilder<PilotProfile> builder)
    {
        builder.ToTable("pilot_profiles");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.UserId).HasMaxLength(450).IsRequired();
        builder.Property(p => p.LicenseNumber).HasMaxLength(50);
        builder.Property(p => p.AircraftTypeRatings).HasMaxLength(200);
        builder.Property(p => p.BaseAirportIcao).HasMaxLength(10);
        builder.Property(p => p.SecondaryAirportIcao).HasMaxLength(10);

        builder.HasOne(p => p.User)
               .WithOne(u => u.PilotProfile)
               .HasForeignKey<PilotProfile>(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.BaseAirport)
               .WithMany()
               .HasForeignKey(p => p.BaseAirportIcao)
               .HasPrincipalKey(a => a.Ident)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
