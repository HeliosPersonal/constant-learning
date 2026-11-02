using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class EntryVersionConfiguration : IEntityTypeConfiguration<EntryVersion>
{
    public void Configure(EntityTypeBuilder<EntryVersion> builder)
    {
        builder.ToTable("entry_versions");
        builder.HasKey(version => version.Id).HasName("entry_versions_pkey");
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.LemmaPl).HasMaxLength(200).IsRequired();
        builder.Property(version => version.PartOfSpeech).HasMaxLength(32).IsRequired();
        builder.Property(version => version.Gender).HasMaxLength(8);
        builder.Property(version => version.GenderPairId).HasMaxLength(64);
        builder.Property(version => version.AspectPairId).HasMaxLength(64);
        builder.Property(version => version.PronunciationUk).HasMaxLength(200).IsRequired();
        builder.Property(version => version.ExamplePl).HasMaxLength(500).IsRequired();
        builder.Property(version => version.ExampleUk).HasMaxLength(500).IsRequired();
        builder.Property(version => version.LegalizationStage).HasMaxLength(32);
        builder.Property(version => version.Topic).HasMaxLength(32);
        builder.Property(version => version.ValidationStatus).HasMaxLength(16).IsRequired();
        builder.Property(version => version.CreatedByKind).HasMaxLength(24).IsRequired();
        builder.Property(version => version.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(version => version.ReviewNote).HasMaxLength(500);
        builder.Property(version => version.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(version => version.Version).IsRowVersion();
        builder.HasIndex(version => new { version.EntryId, version.CreatedAt })
            .HasDatabaseName("ix_entry_versions_entry_id");
        builder.HasIndex(version => version.BasedOnVersionId)
            .HasDatabaseName("ix_entry_versions_based_on_version_id");
        builder.HasIndex(version => version.ContentReportId)
            .HasDatabaseName("ix_entry_versions_content_report_id");
        builder.HasOne(version => version.Entry).WithMany()
            .HasForeignKey(version => version.EntryId)
            .HasConstraintName("entry_versions_entry_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(version => version.BasedOnVersion).WithMany()
            .HasForeignKey(version => version.BasedOnVersionId)
            .HasConstraintName("entry_versions_based_on_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ContentReport>().WithMany()
            .HasForeignKey(version => version.ContentReportId)
            .HasConstraintName("fk_entry_versions_content_report")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
