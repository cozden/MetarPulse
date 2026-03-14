using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class MagicLinkTokenConfiguration : IEntityTypeConfiguration<MagicLinkToken>
{
    public void Configure(EntityTypeBuilder<MagicLinkToken> builder)
    {
        builder.ToTable("magic_link_tokens");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.Email);
        builder.HasIndex(t => t.ExpiresAt);

        builder.Property(t => t.Email).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Token).HasMaxLength(200).IsRequired();
    }
}
