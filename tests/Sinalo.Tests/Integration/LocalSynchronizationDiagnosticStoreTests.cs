using System.IO;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;
using Sinalo.Infrastructure;

namespace Sinalo.Tests.Integration;

public sealed class LocalSynchronizationDiagnosticStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "Sinalo.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RecordAsync_ShouldPersistSanitizedDiagnosticInTheLocalLogsFolder()
    {
        var paths = new LocalSinaloPathService(rootPath: _rootPath);
        var store = new LocalSynchronizationDiagnosticStore(paths);
        var diagnostic = SynchronizationFailureClassifier.Classify(
            new InvalidOperationException("token=secret"), ContentSource.Missions, "Informativo das Missões",
            "https://user:password@example.test/quarter?token=secret", SynchronizationStage.Discovery);

        await store.RecordAsync(diagnostic);

        var log = Assert.Single(Directory.GetFiles(paths.GetPaths().LogsPath, "synchronization-*.jsonl"));
        var text = await File.ReadAllTextAsync(log);
        Assert.Contains("Informativo das Missões", text);
        Assert.Contains("https://example.test/quarter", text);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordAsync_ShouldRemoveExpiredLogFiles()
    {
        var paths = new LocalSinaloPathService(rootPath: _rootPath);
        paths.EnsureFolders();
        var expired = Path.Combine(paths.GetPaths().LogsPath, "synchronization-2000-01-01.jsonl");
        await File.WriteAllTextAsync(expired, "antigo");
        File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-31));
        var store = new LocalSynchronizationDiagnosticStore(paths);
        var diagnostic = SynchronizationFailureClassifier.Classify(new IOException("Falhou"), ContentSource.Health, "Minuto de Saúde", "https://example.test", SynchronizationStage.Storage);

        await store.RecordAsync(diagnostic);

        Assert.False(File.Exists(expired));
    }

    [Fact]
    public async Task RecordAsync_ShouldKeepTotalDiagnosticLogsBelowTenMegabytes()
    {
        var paths = new LocalSinaloPathService(rootPath: _rootPath);
        paths.EnsureFolders();
        var oversized = Path.Combine(paths.GetPaths().LogsPath, "synchronization-2026-01-01.jsonl");
        await File.WriteAllBytesAsync(oversized, new byte[(10 * 1024 * 1024) + 1]);
        var store = new LocalSynchronizationDiagnosticStore(paths);
        var diagnostic = SynchronizationFailureClassifier.Classify(new IOException("Falhou"), ContentSource.Health, "Minuto de Saúde", "https://example.test", SynchronizationStage.Storage);

        await store.RecordAsync(diagnostic);

        Assert.False(File.Exists(oversized));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true);
    }
}
