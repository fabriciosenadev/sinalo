using Microsoft.Data.Sqlite;
using Sinalo.Application.Appearance;
using Sinalo.Application.Configuration;
using Sinalo.Application.Playback;
using Sinalo.Application.Storage;
using Sinalo.Application.Timer;
using Sinalo.Application.Raffle;
using Sinalo.Domain;

namespace Sinalo.Infrastructure;

public sealed class SqliteConfigurationService(ISinaloPathService pathService) : ISinaloConfigurationService, IPlaybackConfigurationService, IThemePreferenceService, ITimerConfigurationService, IRaffleConfigurationService
{
    private readonly ISinaloPathService _pathService = pathService;

    public async Task<IReadOnlyList<SourceConfiguration>> LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var defaults = DefaultSources();
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT source, page_url, policy, download_previous_saturday, download_current_saturday, download_next_saturday FROM source_configurations;";
        var saved = new Dictionary<ContentSource, (string PageUrl, AvailabilityPolicy Policy, DownloadSelection? Selection)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var selection = reader.IsDBNull(3)
                ? null
                : new DownloadSelection(reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5));
            saved[(ContentSource)reader.GetInt32(0)] = (reader.GetString(1), (AvailabilityPolicy)reader.GetInt32(2), selection);
        }
        return defaults.Select(item =>
        {
            if (!saved.TryGetValue(item.Source, out var value)) return item;
            // Migra a configuração inicial do canal do YouTube para a fonte oficial de MP4.
            if (item.Source == ContentSource.Health && IsLegacyHealthYouTubeUrl(value.PageUrl)) return item;
            return item with { PageUrl = value.PageUrl, Policy = value.Policy, DownloadSelection = value.Selection };
        }).ToArray();
    }

    public async Task SaveSourcesAsync(IReadOnlyList<SourceConfiguration> sources, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var source in sources)
        {
            var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO source_configurations (source, page_url, policy, download_previous_saturday, download_current_saturday, download_next_saturday) VALUES ($source, $pageUrl, $policy, $previous, $current, $next) ON CONFLICT(source) DO UPDATE SET page_url = excluded.page_url, policy = excluded.policy, download_previous_saturday = excluded.download_previous_saturday, download_current_saturday = excluded.download_current_saturday, download_next_saturday = excluded.download_next_saturday;";
            command.Parameters.AddWithValue("$source", (int)source.Source); command.Parameters.AddWithValue("$pageUrl", source.PageUrl.Trim());
            command.Parameters.AddWithValue("$policy", (int)source.Policy);
            command.Parameters.AddWithValue("$previous", source.DownloadSelection is null ? DBNull.Value : source.DownloadSelection.PreviousSaturday);
            command.Parameters.AddWithValue("$current", source.DownloadSelection is null ? DBNull.Value : source.DownloadSelection.CurrentSaturday);
            command.Parameters.AddWithValue("$next", source.DownloadSelection is null ? DBNull.Value : source.DownloadSelection.NextSaturday);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PlaybackConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT fullscreen_screen_number, fullscreen_monitor_key FROM playback_configuration WHERE id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new PlaybackConfiguration(null);

        return new PlaybackConfiguration(
            reader.IsDBNull(0) ? null : reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    public async Task SaveAsync(PlaybackConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO playback_configuration (id, fullscreen_screen_number, fullscreen_monitor_key) VALUES (1, $screen, $monitorKey) ON CONFLICT(id) DO UPDATE SET fullscreen_screen_number = excluded.fullscreen_screen_number, fullscreen_monitor_key = excluded.fullscreen_monitor_key;";
        command.Parameters.AddWithValue("$screen", configuration.FullscreenScreenNumber is int screen ? screen : DBNull.Value);
        command.Parameters.AddWithValue("$monitorKey", string.IsNullOrWhiteSpace(configuration.FullscreenMonitorKey) ? DBNull.Value : configuration.FullscreenMonitorKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task<ThemePreference> IThemePreferenceService.LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT theme_preference FROM application_preferences WHERE id = 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? ThemePreference.System : Enum.IsDefined((ThemePreference)Convert.ToInt32(value)) ? (ThemePreference)Convert.ToInt32(value) : ThemePreference.System;
    }

    async Task IThemePreferenceService.SaveAsync(ThemePreference preference, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO application_preferences (id, theme_preference) VALUES (1, $preference) ON CONFLICT(id) DO UPDATE SET theme_preference = excluded.theme_preference;";
        command.Parameters.AddWithValue("$preference", (int)preference);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task<TimerConfiguration> ITimerConfigurationService.LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT direction, countdown_duration_seconds, display_format FROM timer_configuration WHERE id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new(TimerDirection.CountUp, TimeSpan.FromMinutes(1), "hh:mm:ss");
        var direction = Enum.IsDefined((TimerDirection)reader.GetInt32(0)) ? (TimerDirection)reader.GetInt32(0) : TimerDirection.CountUp;
        var duration = TimeSpan.FromSeconds(Math.Max(0, reader.GetInt64(1)));
        return new TimerConfiguration(direction, duration, reader.GetString(2));
    }

    async Task ITimerConfigurationService.SaveAsync(TimerConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO timer_configuration (id, direction, countdown_duration_seconds, display_format) VALUES (1, $direction, $duration, $format) ON CONFLICT(id) DO UPDATE SET direction = excluded.direction, countdown_duration_seconds = excluded.countdown_duration_seconds, display_format = excluded.display_format;";
        command.Parameters.AddWithValue("$direction", (int)configuration.Direction);
        command.Parameters.AddWithValue("$duration", (long)configuration.CountdownDuration.TotalSeconds);
        command.Parameters.AddWithValue("$format", configuration.DisplayFormat);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    async Task<RaffleConfiguration> IRaffleConfigurationService.LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "SELECT animation_duration_seconds FROM raffle_configuration WHERE id = 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return new(TimeSpan.FromSeconds(value is null or DBNull ? 5 : Math.Max(1, Convert.ToInt64(value))));
    }
    async Task IRaffleConfigurationService.SaveAsync(RaffleConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken); var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO raffle_configuration (id, animation_duration_seconds) VALUES (1, $duration) ON CONFLICT(id) DO UPDATE SET animation_duration_seconds = excluded.animation_duration_seconds;";
        command.Parameters.AddWithValue("$duration", (long)configuration.AnimationDuration.TotalSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        _pathService.EnsureFolders();
        var connection = new SqliteConnection($"Data Source={_pathService.GetPaths().DatabasePath}");
        await connection.OpenAsync(cancellationToken); return connection;
    }

    private static IReadOnlyList<SourceConfiguration> DefaultSources() =>
    [new(ContentSource.Missions, "Informativo das Missões", "", AvailabilityPolicy.MonthlyFull), new(ContentSource.ProvaiEVede, "Provai e Vede", "", AvailabilityPolicy.QuarterlyFull), new(ContentSource.Health, "Minuto de Saúde", "https://downloads.adventistas.org/pt/", AvailabilityPolicy.QuarterlyFull)];

    private static bool IsLegacyHealthYouTubeUrl(string pageUrl) => pageUrl.Contains("youtube.com/@VidaeSaudeUCB", StringComparison.OrdinalIgnoreCase);
}
