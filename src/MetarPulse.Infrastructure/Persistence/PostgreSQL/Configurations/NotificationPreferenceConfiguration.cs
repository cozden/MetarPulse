using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(n => n.Id);

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => new { n.UserId, n.StationIcao }).IsUnique();

        builder.Property(n => n.UserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.StationIcao).HasMaxLength(10).IsRequired();
        builder.Property(n => n.TimeZoneId).HasMaxLength(50);

        builder.Property(n => n.ActiveDays)
               .HasColumnType("jsonb");
    }
}
