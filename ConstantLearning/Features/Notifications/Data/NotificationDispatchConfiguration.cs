using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Notifications.Data;

internal sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
    {
        builder.ToTable("notification_dispatches");
        builder.HasKey(dispatch => dispatch.Id).HasName("notification_dispatches_pkey");
        builder.Property(dispatch => dispatch.Id).ValueGeneratedNever();
        builder.Property(dispatch => dispatch.Kind).HasMaxLength(24).IsRequired();
        builder.Property(dispatch => dispatch.Status).HasMaxLength(16).IsRequired();
        builder.Property(dispatch => dispatch.Reason).HasMaxLength(200);
        builder.Property(dispatch => dispatch.TargetLanguage).HasMaxLength(2);
        builder.Property(dispatch => dispatch.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(dispatch => dispatch.Version).IsRowVersion();
        // NULLS NOT DISTINCT: сповіщення без мови (summary, editor digest) лишаються унікальними за (отримувач, вид, дата),
        // а нагадування — за (отримувач, вид, дата, мова), тож дві мови мають окремі рядки в один день (ADR-0005).
        builder.HasIndex(dispatch => new { dispatch.RecipientTelegramUserId, dispatch.Kind, dispatch.LocalDate, dispatch.TargetLanguage })
            .IsUnique().AreNullsDistinct(false).HasDatabaseName("ux_notification_dispatches_recipient_kind_date");
        builder.HasIndex(dispatch => new { dispatch.Status, dispatch.DueAt })
            .HasDatabaseName("ix_notification_dispatches_status_due");
        builder.HasIndex(dispatch => dispatch.LearnerId).HasDatabaseName("ix_notification_dispatches_learner_id");
    }
}
