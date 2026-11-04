using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Identity.Data;

internal sealed class LearnerLanguageStateConfiguration : IEntityTypeConfiguration<LearnerLanguageState>
{
    public void Configure(EntityTypeBuilder<LearnerLanguageState> builder)
    {
        builder.ToTable("learner_language_states");
        builder.HasKey(state => new { state.LearnerId, state.TargetLanguage })
            .HasName("learner_language_states_pkey");
        builder.Property(state => state.TargetLanguage).HasMaxLength(2);
        builder.Property(state => state.DailyNewWordsLimit).IsRequired();
        builder.Property(state => state.CurrentDayTimeZoneAtStart).HasMaxLength(64);
        builder.Property(state => state.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(state => state.Version).IsRowVersion();
        builder.HasIndex(state => state.TargetLanguage)
            .HasDatabaseName("ix_learner_language_states_target_language");
    }
}
