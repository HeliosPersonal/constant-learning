using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateReminderSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS reminder_schedules (
                    learner_id    UUID        NOT NULL PRIMARY KEY REFERENCES learners (id) ON DELETE CASCADE,
                    local_time    TIME        NOT NULL,
                    days_of_week  SMALLINT    NOT NULL,
                    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
                );

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP TABLE IF EXISTS reminder_schedules;

                """);
        }
    }
}
