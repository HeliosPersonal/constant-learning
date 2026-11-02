using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class ValidationEvidenceConfiguration : IEntityTypeConfiguration<ValidationEvidence>
{
    public void Configure(EntityTypeBuilder<ValidationEvidence> builder)
    {
        builder.ToTable("validation_evidence");
        builder.HasKey(evidence => evidence.Id).HasName("validation_evidence_pkey");
        builder.Property(evidence => evidence.Id).ValueGeneratedNever();
        builder.Property(evidence => evidence.Field).HasMaxLength(32).IsRequired();
        builder.Property(evidence => evidence.Source).HasMaxLength(32).IsRequired();
        builder.Property(evidence => evidence.Url).HasColumnType("text").IsRequired();
        builder.Property(evidence => evidence.RevisionId).HasMaxLength(32);
        builder.Property(evidence => evidence.Attribution).HasMaxLength(200);
        builder.Property(evidence => evidence.Outcome).HasMaxLength(16).IsRequired();
        builder.Property(evidence => evidence.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(evidence => evidence.VersionId).HasDatabaseName("ix_validation_evidence_version_id");
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(evidence => evidence.VersionId)
            .HasConstraintName("validation_evidence_version_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
