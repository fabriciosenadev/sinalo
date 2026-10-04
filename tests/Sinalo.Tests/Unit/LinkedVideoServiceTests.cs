using Sinalo.Infrastructure;
using Sinalo.Application.Synchronization;
using Sinalo.Application.Playback;
using System.IO;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class LinkedVideoServiceTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=RN92XFsaPHE")]
    [InlineData("https://youtu.be/RN92XFsaPHE")]
    public void ValidateUrl_AcceptsVideoHosts(string url) => Assert.Equal(url, LinkedVideoService.ValidateUrl(url).AbsoluteUri);

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=RN92XFsaPHE")]
    [InlineData("https://youtube.com.evil.test/watch?v=RN92XFsaPHE")]
    [InlineData("https://example.test/video")]
    public void ValidateUrl_RejectsOtherAddresses(string url) => Assert.Throws<ArgumentException>(() => LinkedVideoService.ValidateUrl(url));

    [Fact]
    public void ValidateUrl_RejectsMissingLink() => Assert.Throws<ArgumentException>(() => LinkedVideoService.ValidateUrl(""));

    [Fact]
    public void ParseMetadata_OffersMp4VideoWithAudioAndStableIdentity()
    {
        const string json = """
            {"id":"RN92XFsaPHE","title":"Minuto de Saúde","upload_date":"20260808","formats":[
              {"format_id":"140","ext":"m4a","vcodec":"none","acodec":"mp4a.40.2","abr":128},
              {"format_id":"18","ext":"mp4","vcodec":"avc1","acodec":"mp4a.40.2","height":360,"filesize":123456},
              {"format_id":"137","ext":"mp4","vcodec":"avc1","acodec":"none","height":1080,"filesize":234567},
              {"format_id":"248","ext":"webm","vcodec":"vp9","acodec":"none","height":1080}
            ]}
            """;
        var result = LinkedVideoService.ParseMetadata(json, new Uri("https://youtu.be/RN92XFsaPHE"));

        Assert.Equal("RN92XFsaPHE", result.Id);
        Assert.Equal("Minuto de Saúde", result.Title);
        Assert.Equal(new DateOnly(2026, 8, 8), result.PublishedDate);
        Assert.Collection(result.Formats,
            format => { Assert.Equal(360, format.Height); Assert.Equal("18", format.Selector); },
            format => { Assert.Equal(1080, format.Height); Assert.Equal("137+140", format.Selector); });
    }

    [Fact]
    public void ParseMetadata_RejectsInvalidIdentityAndMissingPlayableFormat()
    {
        var page = new Uri("https://youtu.be/RN92XFsaPHE");
        Assert.Throws<InvalidDataException>(() => LinkedVideoService.ParseMetadata("""{"id":"bad","formats":[]}""", page));
        Assert.Throws<InvalidDataException>(() => LinkedVideoService.ParseMetadata("""{"id":"RN92XFsaPHE","formats":[{"format_id":"137","ext":"mp4","vcodec":"avc1","acodec":"none","height":1080}]}""", page));
    }

    [Fact]
    public void ParseMetadata_UsesDefaultTitleDateAndApproximateSize()
    {
        const string json = """
            {"id":"RN92XFsaPHE","formats":[{"format_id":"18","ext":"mp4","vcodec":"avc1","acodec":"mp4a","height":360,"filesize_approx":1000000}]}
            """;
        var result = LinkedVideoService.ParseMetadata(json, new Uri("https://youtu.be/RN92XFsaPHE"));
        Assert.Equal("Vídeo RN92XFsaPHE", result.Title);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), result.PublishedDate);
        Assert.Equal(1000000, Assert.Single(result.Formats).EstimatedBytes);
    }

    [Fact]
    public void ParseMetadata_ChoosesOnePlayableMp4PerHeight()
    {
        const string json = """
            {"id":"RN92XFsaPHE","title":"Teste","upload_date":"invalid","formats":[
              {"format_id":"140","ext":"m4a","vcodec":"none","acodec":"mp4a","abr":128},
              {"format_id":"18","ext":"mp4","vcodec":"avc1","acodec":"mp4a","height":360,"filesize":1000},
              {"format_id":"22","ext":"mp4","vcodec":"avc1","acodec":"mp4a","height":360,"filesize":2000},
              {"format_id":"bad","ext":"mp4","vcodec":"none","acodec":"mp4a","height":360},
              {"format_id":"137","ext":"mp4","vcodec":"avc1","acodec":"none","height":1080}
            ]}
            """;
        var video = LinkedVideoService.ParseMetadata(json, new Uri("https://youtu.be/RN92XFsaPHE"));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), video.PublishedDate);
        Assert.Equal("22", video.Formats[0].Selector);
        Assert.Equal("137+140", video.Formats[1].Selector);
    }

    [Fact]
    public void Format_DescribesSizeAndSelector()
    {
        Assert.Equal("360p MP4", new LinkedVideoFormat("18", null, 360, null).Label);
        Assert.Equal("360p MP4", new LinkedVideoFormat("18", null, 360, null).ToString());
        Assert.Contains("MB", new LinkedVideoFormat("137", "140", 1080, 20L * 1024 * 1024).Label);
        Assert.Equal("137+140", new LinkedVideoFormat("137", "140", 1080, null).Selector);
    }

    [Fact]
    public async Task InspectAsync_UsesToolMetadataAndRejectsToolError()
    {
        var paths = new LocalSinaloPathService(rootPath: Path.GetTempPath());
        var service = new LinkedVideoService(paths, toolRunner: (_, _, _) => Task.FromResult(new VideoToolResult(0,
            """{"id":"RN92XFsaPHE","title":"Teste","formats":[{"format_id":"18","ext":"mp4","vcodec":"avc1","acodec":"mp4a","height":360}]}""", "")));
        var video = await service.InspectAsync("https://youtu.be/RN92XFsaPHE");
        Assert.Equal("Teste", video.Title);

        var failing = new LinkedVideoService(paths, toolRunner: (_, _, _) => Task.FromResult(new VideoToolResult(1, "", "Vídeo indisponível")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => failing.InspectAsync("https://youtu.be/RN92XFsaPHE"));
        Assert.Contains("indisponível", error.Message);
    }

    [Fact]
    public async Task DownloadAsync_RetriesThenPublishesOnlyTheValidatedMp4()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new LocalSinaloPathService(rootPath: root);
            var format = new LinkedVideoFormat("18", null, 360, 2048);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var request = HealthRequest(video, format);
            var attempts = 0;
            var stages = new List<string>();
            File.WriteAllBytes(Path.Combine(root, "ffmpeg.exe"), [1]);
            var service = new LinkedVideoService(paths, toolDirectory: root, toolRunner: (arguments, _, _) =>
            {
                Assert.Contains("--ffmpeg-location", arguments);
                attempts++;
                var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                var output = template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4");
                if (attempts == 1)
                {
                    File.WriteAllBytes(output + ".part", new byte[64]);
                    return Task.FromResult(new VideoToolResult(1, "", "Conexão interrompida"));
                }
                var bytes = new byte[2048];
                System.Text.Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes, 4);
                File.WriteAllBytes(output, bytes);
                return Task.FromResult(new VideoToolResult(0, "", ""));
            });

            var ready = await service.DownloadAsync(request, new ImmediateProgress(update => stages.Add(update.Stage)));
            Assert.Equal(2, attempts);
            Assert.True(ready.IsReadyOffline);
            Assert.True(ready.IsPinned);
            Assert.True(File.Exists(ready.LocalPath));
            Assert.Equal(2048, ready.Assets.Single().ExpectedSizeBytes);
            Assert.Equal(64, ready.Assets.Single().Sha256?.Length);
            Assert.Contains("Disponível offline", stages);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(ContentSource.Missions, "missions")]
    [InlineData(ContentSource.ProvaiEVede, "provai-e-vede")]
    [InlineData(ContentSource.Health, "health")]
    public async Task DownloadAsync_StoresVideoInSelectedProgramAndScheduledQuarter(ContentSource destination, string directory)
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, 2048);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var request = new LinkedVideoDownloadRequest(video, format, destination, new DateOnly(2026, 10, 3));
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, toolRunner: (arguments, _, _) =>
            {
                var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                var bytes = new byte[2048];
                System.Text.Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes, 4);
                File.WriteAllBytes(template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4"), bytes);
                return Task.FromResult(new VideoToolResult(0, "", ""));
            });

            var ready = await service.DownloadAsync(request);

            Assert.Equal(request.ItemId, ready.Id);
            Assert.Equal(destination, ready.Source);
            Assert.Equal(new DateOnly(2026, 10, 3), ready.ScheduledDate);
            Assert.Contains(Path.Combine("2026-T4", directory), ready.LocalPath);
            Assert.True(File.Exists(ready.LocalPath));
            Assert.True(ready.IsPinned);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_KeepsPartWhenCancelled()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, 2048);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, toolRunner: (arguments, _, token) =>
            {
                var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                File.WriteAllBytes(template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4") + ".part", new byte[64]);
                throw new OperationCanceledException(token);
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DownloadAsync(HealthRequest(video, format)));
            Assert.Single(Directory.EnumerateFiles(root, "*.part", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_RejectsDifferentFormatAndMissingMerger()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var combined = new LinkedVideoFormat("18", null, 360, null);
            var separate = new LinkedVideoFormat("137", "140", 1080, null);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [combined, separate]);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, toolRunner: (_, _, _) => throw new Exception("Não deveria executar"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadAsync(HealthRequest(video, new LinkedVideoFormat("22", null, 720, null))));
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.DownloadAsync(HealthRequest(video, separate)));
            var invalidId = video with { Id = "../escape" };
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(HealthRequest(invalidId, combined)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_RejectsCorruptVideoAndKeepsItOutOfLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, null);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, toolRunner: (arguments, _, _) =>
            {
                var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                File.WriteAllBytes(template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4"), new byte[2048]);
                return Task.FromResult(new VideoToolResult(0, "", ""));
            });
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(HealthRequest(video, format)));
            Assert.False(File.Exists(Path.Combine(root, "content", "2026-T3", "health", "youtube-RN92XFsaPHE.mp4")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_WaitsWhilePlaybackIsActive()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var gate = new PlaybackActivityGate();
            gate.SetActive(true);
            var format = new LinkedVideoFormat("18", null, 360, null);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, playbackGate: gate, toolRunner: (_, _, _) => { started.TrySetResult(); return Task.FromResult(new VideoToolResult(1, "", "Rede indisponível")); });
            using var cancellation = new CancellationTokenSource();
            var task = service.DownloadAsync(HealthRequest(video, format), cancellationToken: cancellation.Token);
            Assert.False(started.Task.IsCompleted);
            gate.SetActive(false);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_PausesRunningTransferAndResumesAfterPlayback()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var gate = new PlaybackActivityGate();
            var format = new LinkedVideoFormat("18", null, 360, 2048);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var stages = new List<string>();
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, playbackGate: gate, toolRunner: async (arguments, report, token) =>
            {
                calls++;
                var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                var output = template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4");
                if (calls == 1)
                {
                    File.WriteAllBytes(output + ".part", new byte[64]);
                    firstStarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                var bytes = new byte[2048];
                System.Text.Encoding.ASCII.GetBytes("ftyp").CopyTo(bytes, 4);
                File.WriteAllBytes(output, bytes);
                report?.Invoke("[download] 100.0%");
                return new VideoToolResult(0, "", "");
            });
            var task = service.DownloadAsync(HealthRequest(video, format), new ImmediateProgress(update => stages.Add(update.Stage)));
            await firstStarted.Task;
            gate.SetActive(true);
            await Task.Delay(30);
            Assert.False(task.IsCompleted);
            gate.SetActive(false);
            var ready = await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(ready.IsReadyOffline);
            Assert.Equal(2, calls);
            Assert.Contains("Pausado durante a reprodução", stages);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_PreservesPartAfterAllNetworkAttemptsFail()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, null);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var attempts = 0;
            var stages = new List<string>();
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root,
                toolRunner: (arguments, _, _) =>
                {
                    attempts++;
                    var template = arguments[arguments.ToList().IndexOf("--output") + 1];
                    File.WriteAllBytes(template.Replace("%(id)s", video.Id).Replace("%(ext)s", "mp4") + ".part", new byte[64]);
                    return Task.FromResult(new VideoToolResult(1, "", ""));
                }, retryDelay: (_, _) => Task.CompletedTask);
            var error = await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(HealthRequest(video, format), new ImmediateProgress(update => stages.Add(update.Stage))));
            Assert.Contains("Não foi possível", error.Message);
            Assert.Equal(3, attempts);
            Assert.Single(Directory.EnumerateFiles(root, "*.part", SearchOption.AllDirectories));
            Assert.Contains(stages, stage => stage.Contains("Nova tentativa", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_RejectsSuccessfulToolRunWithoutMp4()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, null);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root, toolRunner: (_, _, _) => Task.FromResult(new VideoToolResult(0, "", "")));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(HealthRequest(video, format)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DownloadAsync_StopsBeforeWritingWhenDiskIsFull()
    {
        var root = Path.Combine(Path.GetTempPath(), "sinalo-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var format = new LinkedVideoFormat("18", null, 360, 2048);
            var video = new LinkedVideo("RN92XFsaPHE", "Teste", new Uri("https://youtu.be/RN92XFsaPHE"), new DateOnly(2026, 8, 8), [format]);
            var service = new LinkedVideoService(new LocalSinaloPathService(rootPath: root), toolDirectory: root,
                toolRunner: (_, _, _) => throw new Exception("Não deveria iniciar"), freeSpace: _ => 0);
            var error = await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(HealthRequest(video, format)));
            Assert.Contains("Espaço insuficiente", error.Message);
        }
        finally { Directory.Delete(root, true); }
    }

    private static LinkedVideoDownloadRequest HealthRequest(LinkedVideo video, LinkedVideoFormat format) =>
        new(video, format, ContentSource.Health, video.PublishedDate);

    private sealed class ImmediateProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
