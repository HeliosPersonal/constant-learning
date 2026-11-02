using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateDictionaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS dictionary_entries (
                    id                    UUID        NOT NULL PRIMARY KEY,
                    external_id           VARCHAR(64) NOT NULL,
                    domain                VARCHAR(16) NOT NULL,
                    published_version_id  UUID        NULL,
                    course_position       INTEGER     NULL,
                    created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_dictionary_entries_external_id ON dictionary_entries (external_id);
                CREATE INDEX IF NOT EXISTS ix_dictionary_entries_course_position ON dictionary_entries (course_position) WHERE published_version_id IS NOT NULL;

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_dictionary_entries_course_position;
                DROP INDEX IF EXISTS ux_dictionary_entries_external_id;
                DROP TABLE IF EXISTS dictionary_entries;

                """);
        }
    }
}
