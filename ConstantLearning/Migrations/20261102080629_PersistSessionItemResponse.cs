using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class PersistSessionItemResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "response_payload",
                table: "session_items",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "response_payload",
                table: "session_items");
        }
    }
}
