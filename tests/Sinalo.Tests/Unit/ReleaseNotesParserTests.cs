using Sinalo.App.ReleaseNotes;

namespace Sinalo.Tests.Unit;

public sealed class ReleaseNotesParserTests
{
    [Fact]
    public void Load_ShouldReadTheVersionHistoryEmbeddedInTheApplication()
    {
        var document = ReleaseNotesLoader.Load();

        Assert.Contains(document.Versions, version => version.Version == "0.1.7");
    }

    [Fact]
    public void Parse_ShouldGroupItemsByVersionAndCategory()
    {
        const string markdown = """
            # Histórico

            ## 1.2.3 - 27/08/2026

            ### Novo

            - Primeiro item
              com continuação.

            ### Corrigido

            - Segundo **item**

            ## Histórico anterior ao versionamento público

            Ignorado.
            """;

        var document = ReleaseNotesParser.Parse(markdown);

        var version = Assert.Single(document.Versions);
        Assert.Equal("1.2.3", version.Version);
        Assert.Equal("27/08/2026", version.Date);
        Assert.Equal(["Primeiro item com continuação."], version.Sections[0].Items);
        Assert.Equal(["Segundo item"], version.Sections[1].Items);
    }
}
