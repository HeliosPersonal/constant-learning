using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class AddContentReportForeignKeyToEntryVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                ALTER TABLE entry_versions
                    ADD CONSTRAINT fk_entry_versions_content_report
                    FOREIGN KEY (content_report_id) REFERENCES content_reports (id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                ALTER TABLE entry_versions DROP CONSTRAINT IF EXISTS fk_entry_versions_content_report;

                """);
        }
    }
}
