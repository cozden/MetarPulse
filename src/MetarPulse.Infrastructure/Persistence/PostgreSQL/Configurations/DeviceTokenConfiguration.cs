using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.Token).IsRequired().HasMaxLength(512);
        builder.Property(d => d.Platform).IsRequired().HasMaxLength(16);
        builder.Property(d => d.UpdatedAt).IsRequired();

        // Bir kullanıcının bir token'ı sadece bir kez kayıtlı olsun (upsert için)
        builder.HasIndex(d => d.Token).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
