namespace Sinalo.App.ReleaseNotes;
public sealed class ReleaseNotesViewModel
{
    public string CurrentVersionLabel {get;}
    public IReadOnlyList<ReleaseNotesEntry> Versions {get;}
    public string LatestVersionLabel {get;}
    public ReleaseNotesViewModel(ReleaseNotesDocument document,string current)
    {
        CurrentVersionLabel=$"Versão instalada: {current}. Histórico disponível offline.";
        Versions=document.Versions.OrderByDescending(item=>System.Version.Parse(item.Version)).Select(item=>new ReleaseNotesEntry(item,current)).ToArray();
        LatestVersionLabel = Versions.Count == 0 ? "Nenhum histórico de versões disponível nesta instalação." : Versions[0].Version == current ? "O histórico inclui a versão instalada." : $"Versão mais recente neste histórico: {Versions[0].Version}. Isso não é uma consulta online de atualização.";
    }
}
public sealed class ReleaseNotesEntry(ReleaseNotesVersion notes,string current)
{
    public string Version=>notes.Version;
    public string Date=>notes.Date;
    public IReadOnlyList<ReleaseNotesSection> Sections=>notes.Sections;
    public string Heading=>$"Versão {Version}{(Version==current?" · Instalada":"")} · {Date}";
    public bool IsExpanded {get;set;}=notes.Version==current;
}
