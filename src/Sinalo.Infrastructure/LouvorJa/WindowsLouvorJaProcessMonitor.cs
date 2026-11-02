using System.ComponentModel;
using System.Diagnostics;
using Sinalo.Application.LouvorJa;

namespace Sinalo.Infrastructure.LouvorJa;

public sealed class WindowsLouvorJaProcessMonitor : ILouvorJaProcessMonitor
{
    private int? _trackedProcess;
    public Task<LouvorJaProcessObservation> InspectAsync(string executable, CancellationToken token = default) => Task.Run(() =>
    {
        var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(executable.Trim()));
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || !Path.GetFileName(path).Equals("Louvor JA.exe", StringComparison.OrdinalIgnoreCase))
            return new LouvorJaProcessObservation(LouvorJaProcessState.Unknown, Detail: "Selecione um executável local Louvor JA.exe.");
        var uncertain = false;
        int? match = null;
        foreach (var process in Process.GetProcessesByName("Louvor JA"))
        {
            using (process)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (process.HasExited) continue;
                    var processPath = process.MainModule?.FileName;
                    if (processPath is null) { uncertain = true; continue; }
                    if (Path.GetFullPath(processPath).Equals(path, StringComparison.OrdinalIgnoreCase) && !process.HasExited)
                        if (match is null || process.Id == _trackedProcess) match = process.Id;
                }
                catch (InvalidOperationException) { /* Exited while being inspected. */ }
                catch (Win32Exception) { uncertain = true; }
            }
        }
        _trackedProcess = match;
        if (match is not null) return new(LouvorJaProcessState.Running, match);
        return uncertain
            ? new LouvorJaProcessObservation(LouvorJaProcessState.Unknown, Detail: "Não foi possível conferir o caminho de um processo Louvor JA. Execute ambos com o mesmo nível de permissão.")
            : new LouvorJaProcessObservation(LouvorJaProcessState.Closed);
    }, token);
    public Task StartAsync(string executable, CancellationToken token = default) => LouvorJaApplicationLauncher.StartAsync(executable, token);
}
