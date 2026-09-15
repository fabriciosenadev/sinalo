using System.Text;
using System.Text.Json;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;

namespace Sinalo.Infrastructure;

public sealed class LocalSynchronizationDiagnosticStore(ISinaloPathService pathService) : ISynchronizationDiagnosticStore
{
    private const long MaximumTotalBytes = 10L * 1024 * 1024;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RecordAsync(SynchronizationDiagnostic diagnostic, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            pathService.EnsureFolders();
            var logsPath = pathService.GetPaths().LogsPath;
            Directory.CreateDirectory(logsPath);
            await CleanAsync(logsPath, cancellationToken);

            var filePath = Path.Combine(logsPath, $"synchronization-{DateTime.Today:yyyy-MM-dd}.jsonl");
            var line = JsonSerializer.Serialize(new
            {
                occurredAt = diagnostic.OccurredAt,
                source = diagnostic.SourceName,
                stage = diagnostic.Stage.ToString(),
                category = diagnostic.Category.ToString(),
                sourceUrl = diagnostic.SourceUrl,
                httpStatusCode = diagnostic.HttpStatusCode,
                diagnostic.TechnicalDetails
            }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine, Encoding.UTF8, cancellationToken);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally
        {
            _gate.Release();
        }
    }

    private static Task CleanAsync(string logsPath, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(logsPath, "synchronization-*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderBy(info => info.LastWriteTimeUtc)
            .ToList();

        foreach (var file in files.Where(file => file.LastWriteTimeUtc < DateTime.UtcNow - Retention))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryDelete(file);
        }

        var total = files.Where(file => file.Exists).Sum(file => file.Length);
        foreach (var file in files.Where(file => file.Exists).OrderBy(file => file.LastWriteTimeUtc))
        {
            if (total <= MaximumTotalBytes) break;
            total -= file.Length;
            TryDelete(file);
        }

        return Task.CompletedTask;
    }

    private static void TryDelete(FileInfo file)
    {
        try { file.Delete(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
