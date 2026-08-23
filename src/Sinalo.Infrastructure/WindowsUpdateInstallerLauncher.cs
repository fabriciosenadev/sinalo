using System.Diagnostics;
using Sinalo.Application.Storage;
using Sinalo.Application.Updates;

namespace Sinalo.Infrastructure;

public sealed class WindowsUpdateInstallerLauncher(ISinaloPathService paths) : IUpdateInstallerLauncher
{
    public void Launch(string installerPath)
    {
        if (!File.Exists(installerPath)) throw new FileNotFoundException("O instalador baixado não foi encontrado.", installerPath);
        var updaterSource = Path.Combine(AppContext.BaseDirectory, "updater", "Sinalo.Updater.exe");
        if (!File.Exists(updaterSource)) throw new FileNotFoundException("O componente de atualização não está instalado.", updaterSource);
        var updateDirectory = Path.Combine(paths.GetPaths().RootPath, "updates");
        Directory.CreateDirectory(updateDirectory);
        var updaterTarget = Path.Combine(updateDirectory, "Sinalo.Updater.exe");
        File.Copy(updaterSource, updaterTarget, true);
        var appPath = Path.Combine(AppContext.BaseDirectory, "Sinalo.App.exe");
        _ = Process.Start(new ProcessStartInfo(updaterTarget, $"--parent-pid {Environment.ProcessId} --installer \"{installerPath}\" --app \"{appPath}\"") { UseShellExecute = true })
            ?? throw new InvalidOperationException("Não foi possível iniciar o atualizador.");
    }
}
