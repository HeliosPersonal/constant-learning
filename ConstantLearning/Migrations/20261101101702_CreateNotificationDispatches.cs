using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateNotificationDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS notification_dispatches (
                    id                           UUID         NOT NULL PRIMARY KEY,
                    recipient_telegram_user_id   BIGINT       NOT NULL,
                    learner_id                   UUID         NULL REFERENCES learners (id) ON DELETE CASCADE,
                    kind                         VARCHAR(24)  NOT NULL,
                    local_date                   DATE         NOT NULL,
                    due_at                       TIMESTAMPTZ  NOT NULL,
                    status                       VARCHAR(16)  NOT NULL,
                    attempts                     SMALLINT     NOT NULL,
                    claimed_at                   TIMESTAMPTZ  NULL,
                    sent_at                      TIMESTAMPTZ  NULL,
                    reason                       VARCHAR(200) NULL,
                    created_at                   TIMESTAMPTZ  NOT NULL DEFAULT now()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_notification_dispatches_recipient_kind_date ON notification_dispatches (recipient_telegram_user_id, kind, local_date);
                CREATE INDEX IF NOT EXISTS ix_notification_dispatches_status_due ON notification_dispatches (status, due_at);
                CREATE INDEX IF NOT EXISTS ix_notification_dispatches_learner_id ON notification_dispatches (learner_id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_notification_dispatches_learner_id;
                DROP INDEX IF EXISTS ix_notification_dispatches_status_due;
                DROP INDEX IF EXISTS ux_notification_dispatches_recipient_kind_date;
                DROP TABLE IF EXISTS notification_dispatches;

                """);
        }
    }
}
