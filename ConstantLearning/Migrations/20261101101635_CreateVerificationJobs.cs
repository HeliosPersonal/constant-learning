using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateVerificationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS verification_jobs (
                    id                 UUID         NOT NULL PRIMARY KEY,
                    content_report_id  UUID         NOT NULL REFERENCES content_reports (id) ON DELETE CASCADE,
                    status             VARCHAR(16)  NOT NULL,
                    attempts           SMALLINT     NOT NULL,
                    next_attempt_at    TIMESTAMPTZ  NOT NULL,
                    last_error         VARCHAR(500) NULL,
                    claimed_at         TIMESTAMPTZ  NULL,
                    completed_at       TIMESTAMPTZ  NULL,
                    created_at         TIMESTAMPTZ  NOT NULL DEFAULT now()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_verification_jobs_content_report ON verification_jobs (content_report_id);
                CREATE INDEX IF NOT EXISTS ix_verification_jobs_status_next_attempt ON verification_jobs (status, next_attempt_at);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_verification_jobs_status_next_attempt;
                DROP INDEX IF EXISTS ux_verification_jobs_content_report;
                DROP TABLE IF EXISTS verification_jobs;

                """);
        }
    }
}
