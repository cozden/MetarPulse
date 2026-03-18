using MetarPulse.Core.Enums;
using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class NotamConfiguration : IEntityTypeConfiguration<Notam>
{
    public void Configure(EntityTypeBuilder<Notam> builder)
    {
        builder.ToTable("notams");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.NotamId)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.AirportIdent)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(n => n.FirIdent)
            .HasMaxLength(10);

        builder.Property(n => n.Subject)
            .HasMaxLength(5);

        builder.Property(n => n.QLine)
            .HasMaxLength(200);

        builder.Property(n => n.LowerLimit)
            .HasMaxLength(20);

        builder.Property(n => n.UpperLimit)
            .HasMaxLength(20);

        builder.Property(n => n.Schedule)
            .HasMaxLength(200);

        builder.Property(n => n.RawText)
            .HasMaxLength(4000);

        builder.Property(n => n.SourceProvider)
            .HasMaxLength(50);

        builder.Property(n => n.Traffic)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.Scope)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.NotamType)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(n => n.VfrImpact)
            .HasConversion<string>()
            .HasMaxLength(20);

        // IsActive hesaplanan property — DB'ye yazılmaz
        builder.Ignore(n => n.IsActive);

        // Sık kullanılan sorgular için index
        builder.HasIndex(n => n.AirportIdent);
        builder.HasIndex(n => new { n.AirportIdent, n.EffectiveTo });
        builder.HasIndex(n => n.NotamId).IsUnique();

        // FK — Airport (null olabilir — airport DB'de yoksa NOTAM yine de saklanır)
        builder.HasOne(n => n.Airport)
            .WithMany()
            .HasForeignKey(n => n.AirportIdent)
            .HasPrincipalKey(a => a.Ident)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
