using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class MorphFormConfiguration : IEntityTypeConfiguration<MorphForm>
{
    public void Configure(EntityTypeBuilder<MorphForm> builder)
    {
        builder.ToTable("morph_forms");
        builder.HasKey(form => form.Id).HasName("morph_forms_pkey");
        builder.Property(form => form.Id).ValueGeneratedNever();
        builder.Property(form => form.Lang).HasMaxLength(2).IsRequired();
        builder.Property(form => form.Source).HasMaxLength(32).IsRequired();
        builder.Property(form => form.Lemma).HasMaxLength(200).IsRequired();
        builder.Property(form => form.Form).HasMaxLength(200).IsRequired();
        builder.Property(form => form.Tags).HasMaxLength(64);
        builder.Property(form => form.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(form => new { form.Lang, form.Lemma }).HasDatabaseName("ix_morph_forms_lang_lemma");
    }
}
