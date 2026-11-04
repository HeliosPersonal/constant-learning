using ConstantLearning.Features.Identity.Data;
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
        builder.Property(report => report.TargetLanguage).HasMaxLength(2).IsRequired();
        builder.Property(report => report.Origin).HasMaxLength(24).IsRequired();
        builder.Property(report => report.Field).HasMaxLength(32);
        builder.Property(report => report.ProposedValue).HasMaxLength(500);
        builder.Property(report => report.Comment).HasMaxLength(1000);
        builder.Property(report => report.SearchQuery).HasMaxLength(200);
        builder.Property(report => report.Status).HasMaxLength(24).IsRequired();
        builder.Property(report => report.Outcome).HasMaxLength(24);
        builder.Property(report => report.RejectionReason).HasMaxLength(32);
        builder.Property(report => report.DedupKey).HasMaxLength(64);
        builder.Property(report => report.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(report => report.Version).IsRowVersion();
        builder.HasIndex(report => new { report.LearnerId, report.CreatedAt })
            .HasDatabaseName("ix_content_reports_learner_created");
        builder.HasIndex(report => new { report.LearnerId, report.ClosedAt })
            .HasFilter("closed_at IS NOT NULL AND summary_dispatch_id IS NULL")
            .HasDatabaseName("ix_content_reports_learner_unsummarized");
        builder.HasIndex(report => new { report.LearnerId, report.DedupKey }).IsUnique()
            .HasFilter("closed_at IS NULL AND dedup_key IS NOT NULL")
            .HasDatabaseName("ux_content_reports_open_dedup");
        builder.HasIndex(report => new { report.Status, report.CreatedAt })
            .HasDatabaseName("ix_content_reports_status_created");
        // Черга редактора з розмежуванням за мовою (AC-11, flow 7); доповнює ix_content_reports_status_created.
        builder.HasIndex(report => new { report.TargetLanguage, report.Status, report.CreatedAt })
            .HasDatabaseName("ix_content_reports_lang_status_created");
        // FK target_language покрито власним індексом.
        builder.HasIndex(report => report.TargetLanguage).HasDatabaseName("ix_content_reports_target_language");
        builder.HasIndex(report => report.EntryId).HasDatabaseName("ix_content_reports_entry_id");
        builder.HasIndex(report => report.VersionId).HasDatabaseName("ix_content_reports_version_id");
        builder.HasIndex(report => report.AnswerEventId).IsUnique()
            .HasDatabaseName("ux_content_reports_answer_event_id");
        builder.HasIndex(report => report.SummaryDispatchId)
            .HasDatabaseName("ix_content_reports_summary_dispatch_id");
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
        builder.HasOne<TargetLanguage>().WithMany()
            .HasForeignKey(report => report.TargetLanguage)
            .HasPrincipalKey(language => language.Code)
            .HasConstraintName("content_reports_target_language_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
