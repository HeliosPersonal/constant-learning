using ConstantLearning.Features.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class DictionaryEntryConfiguration : IEntityTypeConfiguration<DictionaryEntry>
{
    public void Configure(EntityTypeBuilder<DictionaryEntry> builder)
    {
        builder.ToTable("dictionary_entries");
        builder.HasKey(entry => entry.Id).HasName("dictionary_entries_pkey");
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Domain).HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.TargetLanguage).HasMaxLength(2).IsRequired();
        builder.Property(entry => entry.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(entry => entry.Version).IsRowVersion();
        // Унікальність external_id — у межах мови (ADR-0003); провідна колонка target_language покриває FK.
        builder.HasIndex(entry => new { entry.TargetLanguage, entry.ExternalId }).IsUnique()
            .HasDatabaseName("ux_dictionary_entries_lang_external_id");
        // Впорядкування base course — per-language; провідна колонка target_language покриває FK.
        builder.HasIndex(entry => new { entry.TargetLanguage, entry.CoursePosition })
            .HasFilter("published_version_id IS NOT NULL")
            .HasDatabaseName("ix_dictionary_entries_lang_course_position");
        builder.HasIndex(entry => entry.PublishedVersionId)
            .HasDatabaseName("ix_dictionary_entries_published_version_id");
        builder.HasOne(entry => entry.PublishedVersion).WithMany()
            .HasForeignKey(entry => entry.PublishedVersionId)
            .HasConstraintName("fk_dictionary_entries_published_version")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TargetLanguage>().WithMany()
            .HasForeignKey(entry => entry.TargetLanguage)
            .HasPrincipalKey(language => language.Code)
            .HasConstraintName("dictionary_entries_target_language_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
