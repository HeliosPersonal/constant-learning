using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Identity.Data;

internal sealed class LearnerConfiguration : IEntityTypeConfiguration<Learner>
{
    public void Configure(EntityTypeBuilder<Learner> builder)
    {
        builder.ToTable("learners");
        builder.HasKey(learner => learner.Id).HasName("learners_pkey");
        builder.Property(learner => learner.Id).ValueGeneratedNever();
        builder.Property(learner => learner.FirstName).HasMaxLength(64);
        builder.Property(learner => learner.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(learner => learner.CurrentDayTimeZoneAtStart).HasMaxLength(64);
        builder.Property(learner => learner.ActiveTargetLanguage).HasMaxLength(2);
        builder.Property(learner => learner.ReporterAlias).HasMaxLength(16);
        builder.Property(learner => learner.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(learner => learner.Version).IsRowVersion();
        builder.HasIndex(learner => learner.TelegramUserId).IsUnique()
            .HasDatabaseName("ux_learners_telegram_user_id");
        builder.HasIndex(learner => learner.ReporterAlias).IsUnique()
            .HasDatabaseName("ux_learners_reporter_alias");
        builder.HasIndex(learner => learner.ActiveTargetLanguage)
            .HasDatabaseName("ix_learners_active_target_language");
    }
}
