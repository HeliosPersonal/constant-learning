using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Identity.Data;

internal sealed class TargetLanguageConfiguration : IEntityTypeConfiguration<TargetLanguage>
{
    public void Configure(EntityTypeBuilder<TargetLanguage> builder)
    {
        builder.ToTable("target_languages");
        builder.HasKey(language => language.Code).HasName("target_languages_pkey");
        builder.Property(language => language.Code).HasMaxLength(2).ValueGeneratedNever();
        builder.Property(language => language.DisplayNameUk).HasMaxLength(64).IsRequired();
        builder.Property(language => language.IsEnabled).IsRequired();
        builder.Property(language => language.SortOrder).IsRequired();
        builder.Property(language => language.NormalizationProfile).HasMaxLength(32);
        builder.Property(language => language.CreatedAt).HasDefaultValueSql("now()");
    }
}
