using System.IO;
using System.Text.RegularExpressions;

namespace Sinalo.Tests.Unit;

public sealed class ReleasePackagingContractTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void FfmpegPackageIsPinnedAndLicenseUsesTheSameDefinition()
    {
        var script = File.ReadAllText(Path.Combine(Root, "eng/prepare-video-download-tools.ps1"));
        var definition = Regex.Match(script, @"\$ffmpegPackage\s*=\s*@\{(?<body>[^}]+)\}").Groups["body"].Value;
        Assert.NotEmpty(definition);
        string Field(string name) => Regex.Match(definition, $@"\b{name}\s*=\s*'([^']+)'").Groups[1].Value;
        var url = new Uri(Field("Url"));
        Assert.Equal("https", url.Scheme); Assert.Equal("github.com", url.Host);
        Assert.StartsWith("/yt-dlp/FFmpeg-Builds/releases/download/autobuild-", url.AbsolutePath);
        Assert.EndsWith("/" + Field("Name"), url.AbsolutePath);
        Assert.EndsWith("-win64-gpl.zip", Field("Name"));
        Assert.Matches("^[a-f0-9]{64}$", Field("Hash"));
        Assert.Matches("^[a-f0-9]{64}$", Field("ExeHash"));
        Assert.Equal("ffmpeg.exe", Field("Exe"));
        Assert.Contains("Join-Path $cache $ffmpegPackage.Name", script);
        Assert.DoesNotContain("/download/latest/", script);
        Assert.DoesNotContain("/releases/latest", script);
        var notices = File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md"));
        Assert.Contains(url.Segments[^2].Trim('/'), notices);
    }

    [Fact]
    public void CleanRelease_PreparesMediaToolsBeforeRunningTests()
    {
        var script = File.ReadAllText(Path.Combine(Root, "releaser.ps1"));
        var prepare = script.IndexOf("& $videoToolsScript", StringComparison.Ordinal);
        var tests = script.IndexOf("& $coverageScript", StringComparison.Ordinal);
        Assert.True(prepare >= 0 && prepare < tests, "Os testes reais de mídia precisam do FFmpeg em checkout limpo.");
        var workflow = File.ReadAllText(Path.Combine(Root, ".github/workflows/release.yml"));
        prepare = workflow.IndexOf("- name: Prepare pinned media tools", StringComparison.Ordinal);
        tests = workflow.IndexOf("- name: Generate installer", StringComparison.Ordinal);
        Assert.True(prepare >= 0 && prepare < tests, "Tags anteriores também precisam dos componentes antes do releaser.");
    }

    [Fact]
    public void Recovery_ChecksOutTheExistingTagAndNeverClobbersPublishedAssets()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github/workflows/release.yml"));
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("ref: ${{ inputs.tag || github.ref }}", workflow);
        Assert.Contains("TAG: ${{ env.RELEASE_TAG }}", workflow);
        Assert.Contains("Refuse to overwrite a published release", workflow);
        Assert.DoesNotContain("--clobber", workflow);
    }
}
