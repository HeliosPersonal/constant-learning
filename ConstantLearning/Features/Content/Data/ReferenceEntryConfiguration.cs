using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class ReferenceEntryConfiguration : IEntityTypeConfiguration<ReferenceEntry>
{
    public void Configure(EntityTypeBuilder<ReferenceEntry> builder)
    {
        builder.ToTable("reference_entries");
        builder.HasKey(entry => entry.Id).HasName("reference_entries_pkey");
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Source).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Lemma).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.LemmaFolded).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Field).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Value).HasMaxLength(500).IsRequired();
        builder.Property(entry => entry.Url).HasColumnType("text").IsRequired();
        builder.Property(entry => entry.RevisionId).HasMaxLength(32);
        builder.Property(entry => entry.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(entry => new { entry.Source, entry.LemmaFolded, entry.Field })
            .HasDatabaseName("ix_reference_entries_lookup");
    }
}
