using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Identity.Data;

internal sealed class ReminderScheduleConfiguration : IEntityTypeConfiguration<ReminderSchedule>
{
    public void Configure(EntityTypeBuilder<ReminderSchedule> builder)
    {
        builder.ToTable("reminder_schedules");
        builder.HasKey(schedule => schedule.LearnerId).HasName("reminder_schedules_pkey");
        builder.Property(schedule => schedule.LearnerId).ValueGeneratedNever();
        builder.Property(schedule => schedule.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(schedule => schedule.Version).IsRowVersion();
        builder.HasOne(schedule => schedule.Learner)
            .WithOne(learner => learner.ReminderSchedule)
            .HasForeignKey<ReminderSchedule>(schedule => schedule.LearnerId)
            .HasConstraintName("reminder_schedules_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
