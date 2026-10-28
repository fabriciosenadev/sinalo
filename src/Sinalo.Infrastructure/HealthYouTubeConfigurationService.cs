using Microsoft.Data.Sqlite;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using System.Text.Json;

namespace Sinalo.Infrastructure;

public sealed class HealthYouTubeConfigurationService(ISinaloPathService paths) : IHealthYouTubeConfigurationService
{
    public async Task<HealthYouTubeConfiguration> LoadAsync(CancellationToken token = default)
    {
        await using var connection = new SqliteConnection($"Data Source={paths.GetPaths().DatabasePath}");
        await connection.OpenAsync(token);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT configuration FROM health_youtube_configuration WHERE id=1";
        var json = await cmd.ExecuteScalarAsync(token) as string;
        return json is null ? new() : JsonSerializer.Deserialize<HealthYouTubeConfiguration>(json) ?? new();
    }
    public async Task SaveAsync(HealthYouTubeConfiguration configuration, CancellationToken token = default)
    {
        configuration.Validate();
        var previous = await LoadAsync(token);
        var normalized = configuration with { ChannelUrl = HealthYouTubeConfiguration.NormalizeChannelUrl(configuration.ChannelUrl).AbsoluteUri, Revision = previous.Revision };
        if (normalized != previous) normalized = normalized with { Revision = Guid.NewGuid().ToString("N") };
        await using var connection = new SqliteConnection($"Data Source={paths.GetPaths().DatabasePath}");
        await connection.OpenAsync(token);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO health_youtube_configuration(id,configuration) VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET configuration=excluded.configuration";
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(normalized));
        await cmd.ExecuteNonQueryAsync(token);
    }
}
