-- Staged migration for multi-target-languages. implement-tasks promotes it (re-stamped) into an EF Core migration via migrationBuilder.Sql.

INSERT INTO target_languages (code, display_name_uk, is_enabled, sort_order, normalization_profile) VALUES
    ('pl', 'Польська',      TRUE,  1, 'pl'),
    ('en', 'Англійська',    FALSE, 2, 'en'),
    ('de', 'Німецька',      FALSE, 3, 'de'),
    ('nl', 'Нідерландська', FALSE, 4, 'nl')
ON CONFLICT (code) DO NOTHING;
