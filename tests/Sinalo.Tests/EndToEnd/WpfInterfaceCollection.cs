namespace Sinalo.Tests.EndToEnd;

// WPF/BAML mantém caches globais; janelas de testes não devem iniciar
// simultaneamente em dispatchers STA diferentes no mesmo processo.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfInterfaceCollection
{
    public const string Name = "Interface WPF";
}
