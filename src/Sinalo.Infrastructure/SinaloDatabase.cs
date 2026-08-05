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
                page_url TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS playback_configuration (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                fullscreen_screen_number INTEGER NULL
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
    }

    private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        try { await command.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase)) { }
    }
}
