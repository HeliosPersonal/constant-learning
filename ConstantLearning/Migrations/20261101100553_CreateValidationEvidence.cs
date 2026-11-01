using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateValidationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS validation_evidence (
                    id            UUID         NOT NULL PRIMARY KEY,
                    version_id    UUID         NOT NULL REFERENCES entry_versions (id) ON DELETE CASCADE,
                    field         VARCHAR(32)  NOT NULL,
                    source        VARCHAR(32)  NOT NULL,
                    url           TEXT         NOT NULL,
                    revision_id   VARCHAR(32)  NULL,
                    attribution   VARCHAR(200) NULL,
                    outcome       VARCHAR(16)  NOT NULL,
                    retrieved_at  TIMESTAMPTZ  NOT NULL,
                    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ix_validation_evidence_version_id ON validation_evidence (version_id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_validation_evidence_version_id;
                DROP TABLE IF EXISTS validation_evidence;

                """);
        }
    }
}
