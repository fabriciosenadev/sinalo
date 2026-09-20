using System.Diagnostics;

var options = args.Chunk(2).ToDictionary(pair => pair[0], pair => pair.Length > 1 ? pair[1] : string.Empty, StringComparer.OrdinalIgnoreCase);
if (!int.TryParse(options.GetValueOrDefault("--parent-pid"), out var parentPid) || !options.TryGetValue("--installer", out var installer) || !options.TryGetValue("--app", out var app)) return 1;
if (!await ParentProcessHasExitedAsync(parentPid, TimeSpan.FromSeconds(90))) return 2;
if (!await CloseRemainingApplicationInstancesAsync(app, TimeSpan.FromSeconds(15))) return 3;
using var setup = Process.Start(new ProcessStartInfo(installer, "/SILENT /NORESTART") { UseShellExecute = true });
if (setup is null) return 1;
await setup.WaitForExitAsync();
if (setup.ExitCode == 0 && File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
return setup.ExitCode;

static async Task<bool> ParentProcessHasExitedAsync(int processId, TimeSpan timeout)
{
    try
    {
        using var process = Process.GetProcessById(processId);
        var exited = await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(timeout));
        return exited is not null && process.HasExited;
    }
    catch (ArgumentException) { return true; }
}

static async Task<bool> CloseRemainingApplicationInstancesAsync(string appPath, TimeSpan timeout)
{
    var fullAppPath = Path.GetFullPath(appPath);
    RequestApplicationClose(fullAppPath);
    if (await WaitForApplicationExitAsync(fullAppPath, TimeSpan.FromSeconds(5))) return true;

    ForceApplicationClose(fullAppPath);
    return await WaitForApplicationExitAsync(fullAppPath, timeout - TimeSpan.FromSeconds(5));
}

static void RequestApplicationClose(string appPath)
{
    foreach (var process in FindApplicationProcesses(appPath))
    {
        using (process)
        {
            try { process.CloseMainWindow(); }
            catch { }
        }
    }
}

static void ForceApplicationClose(string appPath)
{
    foreach (var process in FindApplicationProcesses(appPath))
    {
        using (process)
        {
            try { process.Kill(entireProcessTree: true); }
            catch { }
        }
    }
}

static async Task<bool> WaitForApplicationExitAsync(string appPath, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        if (!HasRunningApplicationProcesses(appPath)) return true;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
    }

    return !HasRunningApplicationProcesses(appPath);
}

static bool HasRunningApplicationProcesses(string appPath)
{
    var hasRunningProcess = false;
    foreach (var process in FindApplicationProcesses(appPath))
    {
        using (process) hasRunningProcess = true;
    }

    return hasRunningProcess;
}

static IEnumerable<Process> FindApplicationProcesses(string appPath)
{
    var processName = Path.GetFileNameWithoutExtension(appPath);
    return Process.GetProcessesByName(processName).Where(process =>
    {
        try
        {
            return !process.HasExited && string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), appPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            process.Dispose();
            return false;
        }
    });
}
