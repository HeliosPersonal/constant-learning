using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateContentReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS content_reports (
                    id                    UUID          NOT NULL PRIMARY KEY,
                    learner_id            UUID          NOT NULL REFERENCES learners (id) ON DELETE CASCADE,
                    entry_id              UUID          NULL REFERENCES dictionary_entries (id),
                    version_id            UUID          NULL REFERENCES entry_versions (id),
                    answer_event_id       UUID          NULL REFERENCES answer_events (id),
                    origin                VARCHAR(24)   NOT NULL,
                    field                 VARCHAR(32)   NULL,
                    proposed_value        VARCHAR(500)  NULL,
                    comment               VARCHAR(1000) NULL,
                    search_query          VARCHAR(200)  NULL,
                    status                VARCHAR(24)   NOT NULL,
                    outcome               VARCHAR(24)   NULL,
                    rejection_reason      VARCHAR(32)   NULL,
                    resulting_version_id  UUID          NULL REFERENCES entry_versions (id),
                    closed_at             TIMESTAMPTZ   NULL,
                    created_at            TIMESTAMPTZ   NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ix_content_reports_learner_created ON content_reports (learner_id, created_at);
                CREATE INDEX IF NOT EXISTS ix_content_reports_learner_closed ON content_reports (learner_id, closed_at);
                CREATE INDEX IF NOT EXISTS ix_content_reports_status_created ON content_reports (status, created_at);
                CREATE INDEX IF NOT EXISTS ix_content_reports_entry_id ON content_reports (entry_id);
                CREATE INDEX IF NOT EXISTS ix_content_reports_version_id ON content_reports (version_id);
                CREATE INDEX IF NOT EXISTS ix_content_reports_answer_event_id ON content_reports (answer_event_id);
                CREATE INDEX IF NOT EXISTS ix_content_reports_resulting_version_id ON content_reports (resulting_version_id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_content_reports_resulting_version_id;
                DROP INDEX IF EXISTS ix_content_reports_answer_event_id;
                DROP INDEX IF EXISTS ix_content_reports_version_id;
                DROP INDEX IF EXISTS ix_content_reports_entry_id;
                DROP INDEX IF EXISTS ix_content_reports_status_created;
                DROP INDEX IF EXISTS ix_content_reports_learner_closed;
                DROP INDEX IF EXISTS ix_content_reports_learner_created;
                DROP TABLE IF EXISTS content_reports;

                """);
        }
    }
}
