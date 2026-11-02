using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedVersionForeignKeyToDictionaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                ALTER TABLE dictionary_entries
                    ADD CONSTRAINT fk_dictionary_entries_published_version
                    FOREIGN KEY (published_version_id) REFERENCES entry_versions (id);
                CREATE INDEX IF NOT EXISTS ix_dictionary_entries_published_version_id ON dictionary_entries (published_version_id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_dictionary_entries_published_version_id;
                ALTER TABLE dictionary_entries DROP CONSTRAINT IF EXISTS fk_dictionary_entries_published_version;

                """);
        }
    }
}
