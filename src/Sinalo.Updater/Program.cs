using System.Diagnostics;

var options = args.Chunk(2).ToDictionary(pair => pair[0], pair => pair.Length > 1 ? pair[1] : string.Empty, StringComparer.OrdinalIgnoreCase);
if (!int.TryParse(options.GetValueOrDefault("--parent-pid"), out var parentPid) || !options.TryGetValue("--installer", out var installer) || !options.TryGetValue("--app", out var app)) return 1;
try { Process.GetProcessById(parentPid).WaitForExit(60000); } catch (ArgumentException) { }
using var setup = Process.Start(new ProcessStartInfo(installer, "/SILENT /NORESTART") { UseShellExecute = true });
if (setup is null) return 1;
await setup.WaitForExitAsync();
if (setup.ExitCode == 0 && File.Exists(app)) Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
return setup.ExitCode;
