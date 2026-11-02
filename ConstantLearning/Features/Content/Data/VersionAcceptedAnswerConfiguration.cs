using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class VersionAcceptedAnswerConfiguration : IEntityTypeConfiguration<VersionAcceptedAnswer>
{
    public void Configure(EntityTypeBuilder<VersionAcceptedAnswer> builder)
    {
        builder.ToTable("version_accepted_answers");
        builder.HasKey(answer => answer.Id).HasName("version_accepted_answers_pkey");
        builder.Property(answer => answer.Id).ValueGeneratedNever();
        builder.Property(answer => answer.TextPl).HasMaxLength(200).IsRequired();
        builder.Property(answer => answer.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(answer => new { answer.VersionId, answer.TextPl }).IsUnique()
            .HasDatabaseName("ux_version_accepted_answers_version_text");
        builder.HasOne(answer => answer.Version).WithMany()
            .HasForeignKey(answer => answer.VersionId)
            .HasConstraintName("version_accepted_answers_version_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
