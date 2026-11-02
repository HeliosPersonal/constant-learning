using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConstantLearning.Migrations
{
    /// <inheritdoc />
    public partial class CreateSessionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                CREATE TABLE IF NOT EXISTS session_items (
                    id                   UUID        NOT NULL PRIMARY KEY,
                    session_id           UUID        NOT NULL REFERENCES learning_sessions (id) ON DELETE CASCADE,
                    entry_id             UUID        NOT NULL REFERENCES dictionary_entries (id),
                    position             SMALLINT    NOT NULL,
                    kind                 VARCHAR(24) NOT NULL,
                    state                VARCHAR(16) NOT NULL,
                    answer_payload_hash  VARCHAR(64) NULL,
                    answered_at          TIMESTAMPTZ NULL,
                    created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_session_items_session_position ON session_items (session_id, position);
                CREATE INDEX IF NOT EXISTS ix_session_items_entry_id ON session_items (entry_id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Staged migration for personal-trainer-mvp. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.
                DROP INDEX IF EXISTS ix_session_items_entry_id;
                DROP INDEX IF EXISTS ux_session_items_session_position;
                DROP TABLE IF EXISTS session_items;

                """);
        }
    }
}
