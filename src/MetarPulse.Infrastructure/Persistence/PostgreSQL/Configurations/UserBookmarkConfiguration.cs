using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Configurations;

public class UserBookmarkConfiguration : IEntityTypeConfiguration<UserBookmark>
{
    public void Configure(EntityTypeBuilder<UserBookmark> builder)
    {
        builder.ToTable("user_bookmarks");
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.UserId);
        builder.HasIndex(b => new { b.UserId, b.StationIcao }).IsUnique();

        builder.Property(b => b.UserId).HasMaxLength(450).IsRequired();
        builder.Property(b => b.StationIcao).HasMaxLength(10).IsRequired();

        builder.HasOne(b => b.Airport)
               .WithMany(a => a.Bookmarks)
               .HasForeignKey(b => b.StationIcao)
               .HasPrincipalKey(a => a.Ident)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
