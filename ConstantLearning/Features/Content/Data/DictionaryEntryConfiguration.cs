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
        builder.Property(entry => entry.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(entry => entry.Version).IsRowVersion();
        builder.HasIndex(entry => entry.ExternalId).IsUnique()
            .HasDatabaseName("ux_dictionary_entries_external_id");
        builder.HasIndex(entry => entry.CoursePosition)
            .HasFilter("published_version_id IS NOT NULL")
            .HasDatabaseName("ix_dictionary_entries_course_position");
        builder.HasIndex(entry => entry.PublishedVersionId)
            .HasDatabaseName("ix_dictionary_entries_published_version_id");
        builder.HasOne(entry => entry.PublishedVersion).WithMany()
            .HasForeignKey(entry => entry.PublishedVersionId)
            .HasConstraintName("fk_dictionary_entries_published_version")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
