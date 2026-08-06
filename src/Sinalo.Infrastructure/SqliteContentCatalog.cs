using Microsoft.Data.Sqlite;
using Sinalo.Application.Catalog;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SqliteContentCatalog(ISinaloPathService pathService) : IContentCatalog
{
    private readonly ISinaloPathService _pathService = pathService;

    public async Task UpsertAsync(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var item in items)
        {
            var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = """
                INSERT INTO content_items (id, source, title, scheduled_date, page_url, sync_state, local_path, is_pinned, updated_at_utc)
                VALUES ($id, $source, $title, $scheduledDate, $pageUrl, $syncState, $localPath, $isPinned, $updatedAtUtc)
                ON CONFLICT(id) DO UPDATE SET
                    source = excluded.source,
                    title = excluded.title,
                    scheduled_date = excluded.scheduled_date,
                    page_url = excluded.page_url,
                    sync_state = excluded.sync_state,
                    local_path = excluded.local_path,
                    is_pinned = excluded.is_pinned,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            itemCommand.Parameters.AddWithValue("$id", item.Id);
            itemCommand.Parameters.AddWithValue("$source", (int)item.Source);
            itemCommand.Parameters.AddWithValue("$title", item.Title);
            itemCommand.Parameters.AddWithValue("$scheduledDate", item.ScheduledDate.ToString("yyyy-MM-dd"));
            itemCommand.Parameters.AddWithValue("$pageUrl", item.PageUri.AbsoluteUri);
            itemCommand.Parameters.AddWithValue("$syncState", (int)item.SyncState);
            itemCommand.Parameters.AddWithValue("$localPath", (object?)item.LocalPath ?? DBNull.Value);
            itemCommand.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);
            itemCommand.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await itemCommand.ExecuteNonQueryAsync(cancellationToken);

            var deleteAssets = connection.CreateCommand();
            deleteAssets.Transaction = transaction;
            deleteAssets.CommandText = "DELETE FROM content_assets WHERE content_item_id = $contentItemId;";
            deleteAssets.Parameters.AddWithValue("$contentItemId", item.Id);
            await deleteAssets.ExecuteNonQueryAsync(cancellationToken);

            foreach (var asset in item.Assets)
            {
                var assetCommand = connection.CreateCommand();
                assetCommand.Transaction = transaction;
                assetCommand.CommandText = """
                    INSERT INTO content_assets (id, content_item_id, download_url, file_name, expected_size_bytes, sha256)
                    VALUES ($id, $contentItemId, $downloadUrl, $fileName, $expectedSizeBytes, $sha256);
                    """;
                assetCommand.Parameters.AddWithValue("$id", asset.Id);
                assetCommand.Parameters.AddWithValue("$contentItemId", item.Id);
                assetCommand.Parameters.AddWithValue("$downloadUrl", asset.DownloadUri.AbsoluteUri);
                assetCommand.Parameters.AddWithValue("$fileName", asset.FileName);
                assetCommand.Parameters.AddWithValue("$expectedSizeBytes", (object?)asset.ExpectedSizeBytes ?? DBNull.Value);
                assetCommand.Parameters.AddWithValue("$sha256", (object?)asset.Sha256 ?? DBNull.Value);
                await assetCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentItem>> ListBySourceAsync(ContentSource source, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, scheduled_date, page_url, sync_state, is_pinned, local_path, play_count, first_played_at_utc, last_played_at_utc FROM content_items WHERE source = $source ORDER BY scheduled_date;";
        command.Parameters.AddWithValue("$source", (int)source);
        var items = new List<ContentItem>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemId = reader.GetString(0);
            items.Add(new ContentItem(
                itemId,
                source,
                reader.GetString(1),
                DateOnly.Parse(reader.GetString(2)),
                new Uri(reader.GetString(3)),
                await ListAssetsAsync(connection, itemId, cancellationToken),
                (SyncState)reader.GetInt32(4),
                reader.GetInt32(5) == 1, reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt32(7), reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)), reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))));
        }

        return items;
    }

    public async Task<ContentItem?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT source FROM content_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var source = await command.ExecuteScalarAsync(cancellationToken);
        if (source is null) return null;
        return (await ListBySourceAsync((ContentSource)Convert.ToInt32(source), cancellationToken)).SingleOrDefault(item => item.Id == id);
    }

    public async Task RecordPlaybackAsync(string id, DateTimeOffset playedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE content_items SET play_count = play_count + 1, first_played_at_utc = COALESCE(first_played_at_utc, $playedAtUtc), last_played_at_utc = $playedAtUtc WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$playedAtUtc", playedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var assets = connection.CreateCommand(); assets.Transaction = transaction;
        assets.CommandText = "DELETE FROM content_assets WHERE content_item_id = $id;";
        assets.Parameters.AddWithValue("$id", id);
        await assets.ExecuteNonQueryAsync(cancellationToken);
        var item = connection.CreateCommand(); item.Transaction = transaction;
        item.CommandText = "DELETE FROM content_items WHERE id = $id;";
        item.Parameters.AddWithValue("$id", id);
        await item.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MediaAsset>> ListAssetsAsync(SqliteConnection connection, string contentItemId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, download_url, file_name, expected_size_bytes, sha256 FROM content_assets WHERE content_item_id = $contentItemId;";
        command.Parameters.AddWithValue("$contentItemId", contentItemId);
        var assets = new List<MediaAsset>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assets.Add(new MediaAsset(
                reader.GetString(0),
                new Uri(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return assets;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        _pathService.EnsureFolders();
        var connection = new SqliteConnection($"Data Source={_pathService.GetPaths().DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
