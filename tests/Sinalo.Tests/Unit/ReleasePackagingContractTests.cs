using System.IO;

namespace Sinalo.Tests.Unit;

public sealed class ReleasePackagingContractTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

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
