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
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
