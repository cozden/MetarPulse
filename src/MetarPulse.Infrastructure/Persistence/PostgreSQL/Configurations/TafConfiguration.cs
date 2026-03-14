using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class TafConfiguration : IEntityTypeConfiguration<Taf>
{
    public void Configure(EntityTypeBuilder<Taf> builder)
    {
        builder.ToTable("taf_history");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.StationId);
        builder.HasIndex(t => t.IssueTime);

        builder.Property(t => t.StationId).HasMaxLength(10).IsRequired();
        builder.Property(t => t.RawText).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.SourceProvider).HasMaxLength(30);

        builder.Property(t => t.Periods)
               .HasColumnType("jsonb");
    }
}
