using System.Diagnostics;
using System.Net;

namespace Sinalo.Infrastructure.LouvorJa;

/// <summary>Only numeric loopback, no proxy, redirects, DNS or remote hosts.</summary>
public sealed class LouvorJaLoopbackHandler : DelegatingHandler
{
    public LouvorJaLoopbackHandler(HttpMessageHandler? inner = null) : base(inner ?? new SocketsHttpHandler
    { UseProxy = false, AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var uri = request.RequestUri;
        if (uri is null || uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.UserInfo.Length != 0)
            throw new HttpRequestException("A integração aceita somente HTTP em 127.0.0.1.");
        return base.SendAsync(request, token);
    }
}

public sealed class LouvorJaLocalConnection(Action<string>? diagnostic = null) : IDisposable
{
    private HttpClient? _http;
    public LouvorJaHttpClient Create(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Porta deve estar entre 1 e 65535.");
        _http?.Dispose();
        _http = new(new LouvorJaLoopbackHandler()) { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = Timeout.InfiniteTimeSpan };
        return new(_http, diagnostic) { RequestTimeout = TimeSpan.FromSeconds(3) };
    }
    public void Dispose() => _http?.Dispose();
}

public static class LouvorJaApplicationLauncher
{
    public static string DefaultExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Louvor JA", "Louvor JA.exe");
    public static Task StartAsync(string executable, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(executable.Trim()));
        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || !Path.GetFileName(fullPath).Equals("Louvor JA.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            throw new FileNotFoundException("Selecione o executável Louvor JA.exe instalado. Nenhum download será realizado.");
        // The app has a single-instance lock; a second launch restores the existing window.
        using var process = Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(fullPath)! });
        if (process is null) throw new IOException("Não foi possível abrir o Louvor JA.");
    }, token);
}
