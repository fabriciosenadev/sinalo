using Microsoft.Data.Sqlite;
using Sinalo.Application.Storage;

namespace Sinalo.Infrastructure;

public sealed class SinaloDatabase(ISinaloPathService pathService)
{
    private readonly ISinaloPathService _pathService = pathService;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _pathService.EnsureFolders();
        var databasePath = _pathService.GetPaths().DatabasePath;

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS content_items (
                id TEXT NOT NULL PRIMARY KEY,
                source INTEGER NOT NULL,
                title TEXT NOT NULL,
                scheduled_date TEXT NOT NULL,
                page_url TEXT NOT NULL,
                sync_state INTEGER NOT NULL,
                local_path TEXT NULL,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                updated_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_content_items_scheduled_date
                ON content_items(scheduled_date);

            CREATE TABLE IF NOT EXISTS source_configurations (
                source INTEGER NOT NULL PRIMARY KEY,
                page_url TEXT NOT NULL,
                policy INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS playback_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                fullscreen_screen_number INTEGER NULL
            );

            CREATE TABLE IF NOT EXISTS application_preferences (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                theme_preference INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS content_cleanup_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                is_enabled INTEGER NOT NULL DEFAULT 0,
                retention_months INTEGER NOT NULL DEFAULT 3,
                grace_period_days INTEGER NOT NULL DEFAULT 30,
                last_run_date TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS timer_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                direction INTEGER NOT NULL DEFAULT 0,
                countdown_duration_seconds INTEGER NOT NULL DEFAULT 60,
                display_format TEXT NOT NULL DEFAULT 'hh:mm:ss'
            );
            CREATE TABLE IF NOT EXISTS raffle_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                animation_duration_seconds INTEGER NOT NULL DEFAULT 5
            );

            CREATE TABLE IF NOT EXISTS worship_timer_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                mode INTEGER NOT NULL DEFAULT 0,
                target_time TEXT NOT NULL DEFAULT '10:00',
                duration_seconds INTEGER NOT NULL DEFAULT 2400,
                stop_at_zero INTEGER NOT NULL DEFAULT 1,
                play_opening INTEGER NOT NULL DEFAULT 1,
                play_five_minutes INTEGER NOT NULL DEFAULT 1,
                play_one_minute INTEGER NOT NULL DEFAULT 1,
                selected_audio_cue INTEGER NOT NULL DEFAULT 0,
                audio_volume REAL NOT NULL DEFAULT 0.8
            );

            CREATE TABLE IF NOT EXISTS content_assets (
                id TEXT NOT NULL PRIMARY KEY,
                content_item_id TEXT NOT NULL,
                download_url TEXT NOT NULL,
                file_name TEXT NOT NULL,
                expected_size_bytes INTEGER NULL,
                sha256 TEXT NULL,
                FOREIGN KEY(content_item_id) REFERENCES content_items(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_content_assets_content_item_id
                ON content_assets(content_item_id);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await AddColumnIfMissingAsync(connection, "content_items", "play_count", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "content_items", "first_played_at_utc", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "content_items", "last_played_at_utc", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "source_configurations", "policy", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "source_configurations", "download_previous_saturday", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "source_configurations", "download_current_saturday", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "source_configurations", "download_next_saturday", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "playback_configuration", "fullscreen_monitor_key", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "worship_timer_configuration", "selected_audio_cue", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "worship_timer_configuration", "audio_volume", "REAL NOT NULL DEFAULT 0.8", cancellationToken);
        var library = connection.CreateCommand();
        library.CommandText = """
            CREATE TABLE IF NOT EXISTS library_media (
                id TEXT PRIMARY KEY NOT NULL, content_item_id TEXT UNIQUE NULL,
                display_name TEXT NULL, usage_date TEXT NULL, original_path TEXT NULL,
                local_path TEXT NULL, storage_mode INTEGER NOT NULL DEFAULT 0,
                availability INTEGER NOT NULL DEFAULT 0, added_at_utc TEXT NOT NULL,
                size_bytes INTEGER NULL, sha256 TEXT NULL, play_count INTEGER NOT NULL DEFAULT 0, media_type INTEGER NOT NULL DEFAULT 0,
                first_played_at_utc TEXT NULL, last_played_at_utc TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_library_original ON library_media(original_path COLLATE NOCASE) WHERE content_item_id IS NULL;
            CREATE INDEX IF NOT EXISTS idx_library_local ON library_media(local_path COLLATE NOCASE);
            INSERT OR IGNORE INTO library_media(id,content_item_id,added_at_utc)
              SELECT id,id,updated_at_utc FROM content_items WHERE local_path IS NOT NULL;
            DROP TRIGGER IF EXISTS library_content_insert;
            DROP TRIGGER IF EXISTS library_content_update;
            CREATE TRIGGER library_content_insert AFTER INSERT ON content_items
              WHEN NEW.local_path IS NOT NULL BEGIN
              INSERT INTO library_media(id,content_item_id,added_at_utc) SELECT NEW.id,NEW.id,NEW.updated_at_utc
              WHERE NOT EXISTS(SELECT 1 FROM library_media WHERE id=NEW.id OR content_item_id=NEW.id); END;
            CREATE TRIGGER library_content_update AFTER UPDATE ON content_items
              WHEN NEW.local_path IS NOT NULL BEGIN
              INSERT INTO library_media(id,content_item_id,added_at_utc) SELECT NEW.id,NEW.id,NEW.updated_at_utc
              WHERE NOT EXISTS(SELECT 1 FROM library_media WHERE id=NEW.id OR content_item_id=NEW.id); END;
            """;
        await library.ExecuteNonQueryAsync(cancellationToken);
        await AddColumnIfMissingAsync(connection,"library_media","is_pinned","INTEGER NOT NULL DEFAULT 0",cancellationToken);
        var categories = connection.CreateCommand();
        categories.CommandText = """
            CREATE TABLE IF NOT EXISTS media_categories(id TEXT PRIMARY KEY, name TEXT NOT NULL, name_key TEXT UNIQUE NOT NULL, position INTEGER NOT NULL, is_system INTEGER NOT NULL DEFAULT 0, revision INTEGER NOT NULL DEFAULT 0, deleting INTEGER NOT NULL DEFAULT 0);
            INSERT OR IGNORE INTO media_categories(id,name,name_key,position,is_system) VALUES
              ('missions','Informativo das Missões','INFORMATIVO DAS MISSÕES',0,1),
              ('provai-e-vede','Provai e Vede','PROVAI E VEDE',1,1),
              ('health','Minuto de Saúde','MINUTO DE SAÚDE',2,1),
              ('uncategorized','Sem categoria','SEM CATEGORIA',3,1);
            CREATE TABLE IF NOT EXISTS media_category_assignments(media_id TEXT PRIMARY KEY, category_id TEXT NOT NULL REFERENCES media_categories(id));
            CREATE INDEX IF NOT EXISTS idx_media_category ON media_category_assignments(category_id);
            CREATE TABLE IF NOT EXISTS category_deletions(category_id TEXT NOT NULL, media_id TEXT NOT NULL, path TEXT NULL, storage_mode INTEGER NOT NULL, size INTEGER NULL, modified_ticks INTEGER NULL, removed_file INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(category_id,media_id));
            CREATE TABLE IF NOT EXISTS linked_library_media(media_id TEXT PRIMARY KEY, video_id TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_linked_library_video ON linked_library_media(video_id);
            """;
        await categories.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        try { await command.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase)) { }
    }
}
