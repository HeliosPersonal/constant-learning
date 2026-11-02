using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class PersistSessionResultView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "result_item_id",
                table: "learning_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_learning_sessions_result_item_id ON learning_sessions (result_item_id)", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE learning_sessions ADD CONSTRAINT fk_learning_sessions_result_item_id FOREIGN KEY (result_item_id) REFERENCES session_items (id) ON DELETE RESTRICT NOT VALID");
            migrationBuilder.Sql("ALTER TABLE learning_sessions VALIDATE CONSTRAINT fk_learning_sessions_result_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_learning_sessions_result_item_id",
                table: "learning_sessions");

            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS ix_learning_sessions_result_item_id", suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "result_item_id",
                table: "learning_sessions");
        }
    }
}
