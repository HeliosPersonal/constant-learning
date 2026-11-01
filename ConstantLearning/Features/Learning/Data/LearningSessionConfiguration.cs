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
        builder.Property(session => session.Status).HasMaxLength(16).IsRequired();
        builder.Property(session => session.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(session => session.Version).IsRowVersion();
        builder.HasIndex(session => session.LearnerId).IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("ux_learning_sessions_one_active");
        builder.HasIndex(session => new { session.LearnerId, session.CompletedAt })
            .HasDatabaseName("ix_learning_sessions_learner_completed");
        builder.HasIndex(session => new { session.LearnerId, session.LearnerDay })
            .HasDatabaseName("ix_learning_sessions_learner_day");
    }
}
