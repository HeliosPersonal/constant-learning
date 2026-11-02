using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class SearchFormConfiguration : IEntityTypeConfiguration<SearchForm>
{
    public void Configure(EntityTypeBuilder<SearchForm> builder)
    {
        builder.ToTable("search_forms");
        builder.HasKey(form => form.Id).HasName("search_forms_pkey");
        builder.Property(form => form.Id).ValueGeneratedNever();
        builder.Property(form => form.Lang).HasMaxLength(2).IsRequired();
        builder.Property(form => form.FormDisplay).HasMaxLength(200).IsRequired();
        builder.Property(form => form.FormFolded).HasMaxLength(200).IsRequired();
        builder.Property(form => form.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(form => form.FormFolded).HasOperators("text_pattern_ops")
            .HasDatabaseName("ix_search_forms_form_folded");
        builder.HasIndex(form => form.EntryId).HasDatabaseName("ix_search_forms_entry_id");
        builder.HasIndex(form => form.VersionId).HasDatabaseName("ix_search_forms_version_id");
        builder.HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(form => form.EntryId)
            .HasConstraintName("search_forms_entry_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(form => form.VersionId)
            .HasConstraintName("search_forms_version_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
