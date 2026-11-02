using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class VersionTranslationConfiguration : IEntityTypeConfiguration<VersionTranslation>
{
    public void Configure(EntityTypeBuilder<VersionTranslation> builder)
    {
        builder.ToTable("version_translations");
        builder.HasKey(translation => translation.Id).HasName("version_translations_pkey");
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Property(translation => translation.Kind).HasMaxLength(16).IsRequired();
        builder.Property(translation => translation.TextUk).HasMaxLength(200).IsRequired();
        builder.Property(translation => translation.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(translation => new { translation.VersionId, translation.Position }).IsUnique()
            .HasDatabaseName("ux_version_translations_version_position");
        builder.HasOne(translation => translation.Version).WithMany()
            .HasForeignKey(translation => translation.VersionId)
            .HasConstraintName("version_translations_version_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
