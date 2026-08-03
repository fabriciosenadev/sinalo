using Microsoft.Data.Sqlite;
using Sinalo.Application.Configuration;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SqliteConfigurationService(ISinaloPathService pathService) : ISinaloConfigurationService
{
    private readonly ISinaloPathService _pathService = pathService;

    public async Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var defaults = DefaultSources();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT source, page_url FROM source_configurations;";
        var saved = new Dictionary<ContentSource, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) saved[(ContentSource)reader.GetInt32(0)] = reader.GetString(1);
        return defaults.Select(item => item with { PageUrl = saved.GetValueOrDefault(item.Source, item.PageUrl) }).ToArray();
    }

    public async Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var source in sources)
        {
            var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO source_configurations (source, page_url) VALUES ($source, $pageUrl) ON CONFLICT(source) DO UPDATE SET page_url = excluded.page_url;";
            command.Parameters.AddWithValue("$source", (int)source.Source); command.Parameters.AddWithValue("$pageUrl", source.PageUrl.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        _pathService.EnsureFolders();
        var connection = new SqliteConnection($"Data Source={_pathService.GetPaths().DatabasePath}");
        await connection.OpenAsync(cancellationToken); return connection;
    }

    private static IReadOnlyList<SourceConfiguration> DefaultSources() =>
    [new(ContentSource.Missions, "Informativo das Missões", "", AvailabilityPolicy.MonthlyFull), new(ContentSource.ProvaiEVede, "Provai e Vede", "", AvailabilityPolicy.QuarterlyFull), new(ContentSource.Health, "Minuto de Saúde", "", AvailabilityPolicy.MonthlyFull)];
}
