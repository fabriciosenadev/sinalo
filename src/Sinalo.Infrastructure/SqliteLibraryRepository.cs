using Microsoft.Data.Sqlite;
using Sinalo.Application.Library;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SqliteLibraryRepository(ISinaloPathService paths) : ILibraryRepository
{
    private const string Projection = """
        SELECT m.id,COALESCE(m.display_name,c.title,''),m.content_item_id,c.source,
        CASE WHEN m.content_item_id IS NULL THEN m.usage_date ELSE c.scheduled_date END AS usage,
        CASE WHEN m.content_item_id IS NULL THEN m.local_path ELSE c.local_path END AS path,
        m.original_path,m.storage_mode,m.availability,m.added_at_utc,m.size_bytes,m.sha256,
        CASE WHEN m.content_item_id IS NULL THEN m.play_count ELSE c.play_count END,c.is_pinned,c.sync_state,m.media_type,
        CASE WHEN m.content_item_id IS NULL THEN m.first_played_at_utc ELSE c.first_played_at_utc END,
        CASE WHEN m.content_item_id IS NULL THEN m.last_played_at_utc ELSE c.last_played_at_utc END
        FROM library_media m LEFT JOIN content_items c ON c.id=m.content_item_id
        """;
    private const string Eligible = "(m.content_item_id IS NULL OR (c.id IS NOT NULL AND c.local_path IS NOT NULL))";
    public async Task<LibraryPage> QueryAsync(LibraryQuery query, CancellationToken token = default)
    {
        var search = DateOnly.TryParseExact(query.Search, "dd/MM/yyyy", out var date) ? date.ToString("yyyy-MM-dd") : query.Search;
        var order = query.Sort switch { LibrarySort.AddedAt => "m.added_at_utc DESC", LibrarySort.UsageDate => "usage IS NULL,usage", _ => "COALESCE(m.display_name,c.title,'') COLLATE NOCASE" };
        await using var connection = await OpenAsync(token);
        var where = Eligible + " AND (COALESCE(m.display_name,c.title,'') LIKE $search ESCAPE '\\' OR COALESCE(CASE WHEN m.content_item_id IS NULL THEN m.usage_date ELSE c.scheduled_date END,'') LIKE $search ESCAPE '\\') AND ($origin='Todos' OR ($origin='Importados' AND m.content_item_id IS NULL) OR CAST(c.source AS TEXT)=$origin)";
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM library_media m LEFT JOIN content_items c ON c.id=m.content_item_id WHERE " + where;
        command.Parameters.AddWithValue("$search", "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
        command.Parameters.AddWithValue("$origin", query.Origin);
        var total = Convert.ToInt32(await command.ExecuteScalarAsync(token));
        command.CommandText = Projection + " WHERE " + where + $" ORDER BY {order},m.id LIMIT $limit OFFSET $offset";
        var limit = Math.Clamp(query.PageSize, 1, 100);
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", Math.Max(0, query.Page) * limit);
        return new(await ReadAsync(command, token), total);
    }
    public async Task<LibraryMedia?> FindAsync(string id, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = Projection + " WHERE m.id=$id AND " + Eligible;
        command.Parameters.AddWithValue("$id", id);
        return (await ReadAsync(command, token)).SingleOrDefault();
    }
    public async Task<LibraryMedia?> FindByPathAsync(string path, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = Projection + " WHERE " + Eligible + " AND (m.original_path=$path COLLATE NOCASE OR m.local_path=$path COLLATE NOCASE OR c.local_path=$path COLLATE NOCASE) LIMIT 1";
        command.Parameters.AddWithValue("$path", Path.GetFullPath(path));
        return (await ReadAsync(command, token)).SingleOrDefault();
    }
    private static async Task<List<LibraryMedia>> ReadAsync(SqliteCommand command, CancellationToken token)
    {
        var items = new List<LibraryMedia>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            string? Text(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
            var path = Text(5);
            var availability = (MediaAvailability)reader.GetInt32(8);
            if (!reader.IsDBNull(14) && reader.GetInt32(14) != (int)SyncState.Ready) availability = MediaAvailability.Invalid;
            if (path is null || !File.Exists(path)) availability = MediaAvailability.Missing;
            else if (reader.IsDBNull(2) && !reader.IsDBNull(10) && new FileInfo(path).Length != reader.GetInt64(10)) availability = MediaAvailability.Invalid;
            items.Add(new(reader.GetString(0), reader.GetString(1), Text(2), reader.IsDBNull(3) ? null : (ContentSource)reader.GetInt32(3),
                Text(4) is { } date ? DateOnly.Parse(date) : null, path, Text(6), (MediaStorageMode)reader.GetInt32(7),
                availability, DateTimeOffset.Parse(reader.GetString(9)), reader.IsDBNull(10) ? null : reader.GetInt64(10), Text(11),
                reader.IsDBNull(12) ? 0 : reader.GetInt32(12), !reader.IsDBNull(13) && reader.GetInt32(13) == 1, (LibraryMediaType)reader.GetInt32(15),
                Text(16) is { } first ? DateTimeOffset.Parse(first) : null, Text(17) is { } last ? DateTimeOffset.Parse(last) : null));
        }
        return items;
    }
    public Task SaveImportedAsync(LibraryMedia media, CancellationToken token = default)
    {
        if (!media.IsImported) throw new InvalidOperationException("Conteúdo sincronizado é mantido pelo catálogo original.");
        return ExecuteAsync("INSERT INTO library_media(id,display_name,usage_date,local_path,original_path,storage_mode,availability,added_at_utc,size_bytes,sha256) VALUES($id,$name,$date,$path,$original,$mode,$state,$added,$size,$hash)", token,
            ("$id", media.Id), ("$name", media.Name), ("$date", media.UsageDate?.ToString("yyyy-MM-dd")), ("$path", media.LocalPath),
            ("$original", media.OriginalPath), ("$mode", (int)media.StorageMode), ("$state", (int)media.Availability),
            ("$added", media.AddedAtUtc.ToString("O")), ("$size", media.SizeBytes), ("$hash", media.Sha256));
    }
    public Task RenameAsync(string id, string name, DateOnly? usageDate, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe o nome exibido.");
        return ExecuteAsync("UPDATE library_media SET display_name=$name,usage_date=CASE WHEN content_item_id IS NULL THEN $date ELSE usage_date END WHERE id=$id", token,
            ("$id", id), ("$name", name.Trim()), ("$date", usageDate?.ToString("yyyy-MM-dd")));
    }
    public async Task RelinkAsync(string id, string path, long size, string hash, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE library_media SET local_path=$path,original_path=CASE WHEN content_item_id IS NULL AND storage_mode=1 THEN $path ELSE original_path END,size_bytes=$size,sha256=$hash,availability=0 WHERE id=$id; UPDATE content_items SET local_path=$path,sync_state=3 WHERE id=(SELECT content_item_id FROM library_media WHERE id=$id);";
        command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$path", path); command.Parameters.AddWithValue("$size", size); command.Parameters.AddWithValue("$hash", hash);
        await command.ExecuteNonQueryAsync(token); await transaction.CommitAsync(token);
    }
    public Task RemoveImportedAsync(string id, CancellationToken token = default) => ExecuteAsync("DELETE FROM library_media WHERE id=$id AND content_item_id IS NULL", token, ("$id", id));
    public Task RecordPlaybackAsync(string id, CancellationToken token = default) => ExecuteAsync("UPDATE library_media SET play_count=play_count+1,first_played_at_utc=COALESCE(first_played_at_utc,$time),last_played_at_utc=$time WHERE id=$id AND content_item_id IS NULL", token, ("$id", id),("$time",DateTimeOffset.UtcNow.ToString("O")));
    public async Task RelocateAsync(string previousRoot, string nextRoot, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id,local_path FROM library_media WHERE content_item_id IS NULL AND storage_mode=0 AND local_path IS NOT NULL";
        var updates = new List<(string Id, string Path)>();
        await using (var reader = await command.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token))
            {
                var path = reader.GetString(1);
                if (Path.GetFullPath(path).StartsWith(previousRoot.TrimEnd('\\','/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    updates.Add((reader.GetString(0), Path.Combine(nextRoot, Path.GetRelativePath(previousRoot, path))));
            }
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        foreach (var update in updates)
        {
            command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "UPDATE library_media SET local_path=$path WHERE id=$id";
            command.Parameters.AddWithValue("$path", update.Path); command.Parameters.AddWithValue("$id", update.Id);
            await command.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }
    public async Task<int> CountPathReferencesAsync(string path, CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand(); command.CommandText = "SELECT (SELECT COUNT(*) FROM library_media WHERE content_item_id IS NULL AND local_path=$path COLLATE NOCASE)+(SELECT COUNT(*) FROM content_items WHERE local_path=$path COLLATE NOCASE)";
        command.Parameters.AddWithValue("$path", path); return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }
    private async Task ExecuteAsync(string sql, CancellationToken token, params (string Key, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(token);
        var command = connection.CreateCommand(); command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(token);
    }
    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection($"Data Source={paths.GetPaths().DatabasePath}");
        await connection.OpenAsync(token); return connection;
    }
}
