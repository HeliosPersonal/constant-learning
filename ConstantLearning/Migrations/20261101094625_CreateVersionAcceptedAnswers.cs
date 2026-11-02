using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateVersionAcceptedAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS version_accepted_answers (
                    id            UUID         NOT NULL PRIMARY KEY,
                    version_id    UUID         NOT NULL REFERENCES entry_versions (id) ON DELETE CASCADE,
                    text_pl       VARCHAR(200) NOT NULL,
                    is_canonical  BOOLEAN      NOT NULL,
                    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_version_accepted_answers_version_text ON version_accepted_answers (version_id, text_pl);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ux_version_accepted_answers_version_text;
                DROP TABLE IF EXISTS version_accepted_answers;

                """);
        }
    }
}
