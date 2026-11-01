using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Learning.Data;

internal sealed class AnswerEventConfiguration : IEntityTypeConfiguration<AnswerEvent>
{
    public void Configure(EntityTypeBuilder<AnswerEvent> builder)
    {
        builder.ToTable("answer_events");
        builder.HasKey(answer => answer.Id).HasName("answer_events_pkey");
        builder.Property(answer => answer.Id).ValueGeneratedNever();
        builder.Property(answer => answer.ExerciseKind).HasMaxLength(24).IsRequired();
        builder.Property(answer => answer.Outcome).HasMaxLength(24).IsRequired();
        builder.Property(answer => answer.RawAnswer).HasMaxLength(200);
        builder.Property(answer => answer.NormalizedAnswer).HasMaxLength(200);
        builder.Property(answer => answer.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(answer => answer.SchedulerVersion).HasMaxLength(16).IsRequired();
        builder.Property(answer => answer.StateBefore).HasColumnType("jsonb").IsRequired();
        builder.Property(answer => answer.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(answer => answer.SessionItemId).IsUnique()
            .HasDatabaseName("ux_answer_events_session_item");
        builder.HasIndex(answer => new { answer.LearnerId, answer.EntryId, answer.AnsweredAt })
            .HasDatabaseName("ix_answer_events_learner_entry");
        builder.HasIndex(answer => new { answer.LearnerId, answer.LearnerDay, answer.Outcome })
            .HasDatabaseName("ix_answer_events_learner_day_outcome");
        builder.HasIndex(answer => answer.EntryId).HasDatabaseName("ix_answer_events_entry_id");
        builder.HasIndex(answer => answer.VersionId).HasDatabaseName("ix_answer_events_version_id");
        builder.HasOne(answer => answer.SessionItem).WithOne()
            .HasForeignKey<AnswerEvent>(answer => answer.SessionItemId)
            .HasConstraintName("answer_events_session_item_id_fkey")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
