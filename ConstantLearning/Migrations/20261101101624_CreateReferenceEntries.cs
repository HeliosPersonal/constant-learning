using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateReferenceEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS reference_entries (
                    id            UUID         NOT NULL PRIMARY KEY,
                    source        VARCHAR(32)  NOT NULL,
                    lemma         VARCHAR(200) NOT NULL,
                    lemma_folded  VARCHAR(200) NOT NULL,
                    field         VARCHAR(32)  NOT NULL,
                    value         VARCHAR(500) NOT NULL,
                    url           TEXT         NOT NULL,
                    revision_id   VARCHAR(32)  NULL,
                    retrieved_at  TIMESTAMPTZ  NOT NULL,
                    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now()
                );
                CREATE INDEX IF NOT EXISTS ix_reference_entries_lookup ON reference_entries (source, lemma_folded, field);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_reference_entries_lookup;
                DROP TABLE IF EXISTS reference_entries;

                """);
        }
    }
}
