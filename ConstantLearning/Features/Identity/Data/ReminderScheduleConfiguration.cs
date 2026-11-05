using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Identity.Data;

internal sealed class ReminderScheduleConfiguration : IEntityTypeConfiguration<ReminderSchedule>
{
    public void Configure(EntityTypeBuilder<ReminderSchedule> builder)
    {
        builder.ToTable("reminder_schedules");
        builder.HasKey(schedule => new { schedule.LearnerId, schedule.TargetLanguage })
            .HasName("reminder_schedules_pkey");
        builder.Property(schedule => schedule.TargetLanguage).HasMaxLength(2).IsRequired();
        builder.Property(schedule => schedule.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(schedule => schedule.Version).IsRowVersion();
        builder.HasOne(schedule => schedule.Learner)
            .WithMany(learner => learner.ReminderSchedules)
            .HasForeignKey(schedule => schedule.LearnerId)
            .HasConstraintName("reminder_schedules_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TargetLanguage>().WithMany()
            .HasForeignKey(schedule => schedule.TargetLanguage)
            .HasPrincipalKey(language => language.Code)
            .HasConstraintName("reminder_schedules_target_language_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(schedule => schedule.TargetLanguage)
            .HasDatabaseName("ix_reminder_schedules_target_language");
    }
}
