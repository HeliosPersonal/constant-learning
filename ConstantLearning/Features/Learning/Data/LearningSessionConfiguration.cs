using ConstantLearning.Features.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Learning.Data;

internal sealed class LearningSessionConfiguration : IEntityTypeConfiguration<LearningSession>
{
    public void Configure(EntityTypeBuilder<LearningSession> builder)
    {
        builder.ToTable("learning_sessions");
        builder.HasKey(session => session.Id).HasName("learning_sessions_pkey");
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.TargetLanguage).HasMaxLength(2).IsRequired();
        builder.Property(session => session.Status).HasMaxLength(16).IsRequired();
        builder.Property(session => session.Kind).HasMaxLength(24).IsRequired();
        builder.Property(session => session.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(session => session.Version).IsRowVersion();
        builder.HasIndex(session => session.ResultItemId).HasDatabaseName("ix_learning_sessions_result_item_id");
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(session => session.ResultItemId)
            .HasConstraintName("fk_learning_sessions_result_item_id").OnDelete(DeleteBehavior.Restrict);
        // Інваріант «одна активна session НА МОВУ» (AC-04): замінює глобальний ux_learning_sessions_one_active,
        // який дозволяв лише одну активну session на learner у всіх мовах і блокував старт другої мови.
        builder.HasIndex(session => new { session.LearnerId, session.TargetLanguage }).IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("ux_learning_sessions_one_active_lang");
        builder.HasIndex(session => new { session.LearnerId, session.CompletedAt })
            .HasDatabaseName("ix_learning_sessions_learner_completed");
        builder.HasIndex(session => new { session.LearnerId, session.LearnerDay })
            .HasDatabaseName("ix_learning_sessions_learner_day");
        // FK target_language покрито власним індексом.
        builder.HasIndex(session => session.TargetLanguage).HasDatabaseName("ix_learning_sessions_target_language");
        builder.HasOne<TargetLanguage>().WithMany()
            .HasForeignKey(session => session.TargetLanguage)
            .HasPrincipalKey(language => language.Code)
            .HasConstraintName("learning_sessions_target_language_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
