using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("content_reports");
        builder.HasKey(report => report.Id).HasName("content_reports_pkey");
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.Origin).HasMaxLength(24).IsRequired();
        builder.Property(report => report.Field).HasMaxLength(32);
        builder.Property(report => report.ProposedValue).HasMaxLength(500);
        builder.Property(report => report.Comment).HasMaxLength(1000);
        builder.Property(report => report.SearchQuery).HasMaxLength(200);
        builder.Property(report => report.Status).HasMaxLength(24).IsRequired();
        builder.Property(report => report.Outcome).HasMaxLength(24);
        builder.Property(report => report.RejectionReason).HasMaxLength(32);
        builder.Property(report => report.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(report => report.Version).IsRowVersion();
        builder.HasIndex(report => new { report.LearnerId, report.CreatedAt })
            .HasDatabaseName("ix_content_reports_learner_created");
        builder.HasIndex(report => new { report.LearnerId, report.ClosedAt })
            .HasDatabaseName("ix_content_reports_learner_closed");
        builder.HasIndex(report => new { report.Status, report.CreatedAt })
            .HasDatabaseName("ix_content_reports_status_created");
        builder.HasIndex(report => report.EntryId).HasDatabaseName("ix_content_reports_entry_id");
        builder.HasIndex(report => report.VersionId).HasDatabaseName("ix_content_reports_version_id");
        builder.HasIndex(report => report.AnswerEventId).HasDatabaseName("ix_content_reports_answer_event_id");
        builder.HasIndex(report => report.ResultingVersionId)
            .HasDatabaseName("ix_content_reports_resulting_version_id");
        builder.HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(report => report.EntryId)
            .HasConstraintName("content_reports_entry_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(report => report.VersionId)
            .HasConstraintName("content_reports_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(report => report.ResultingVersionId)
            .HasConstraintName("content_reports_resulting_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
