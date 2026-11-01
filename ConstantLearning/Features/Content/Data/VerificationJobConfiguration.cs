using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class VerificationJobConfiguration : IEntityTypeConfiguration<VerificationJob>
{
    public void Configure(EntityTypeBuilder<VerificationJob> builder)
    {
        builder.ToTable("verification_jobs");
        builder.HasKey(job => job.Id).HasName("verification_jobs_pkey");
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.Status).HasMaxLength(16).IsRequired();
        builder.Property(job => job.LastError).HasMaxLength(500);
        builder.Property(job => job.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(job => job.Version).IsRowVersion();
        builder.HasIndex(job => job.ContentReportId).IsUnique()
            .HasDatabaseName("ux_verification_jobs_content_report");
        builder.HasIndex(job => new { job.Status, job.NextAttemptAt })
            .HasDatabaseName("ix_verification_jobs_status_next_attempt");
        builder.HasOne<ContentReport>().WithOne()
            .HasForeignKey<VerificationJob>(job => job.ContentReportId)
            .HasConstraintName("verification_jobs_content_report_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
