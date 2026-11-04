-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

CREATE TABLE IF NOT EXISTS target_languages (
    code                  VARCHAR(2)  NOT NULL PRIMARY KEY,
    display_name_uk       VARCHAR(64) NOT NULL,
    is_enabled            BOOLEAN     NOT NULL,
    sort_order            SMALLINT    NOT NULL,
    normalization_profile VARCHAR(32) NULL,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);
