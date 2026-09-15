using System.Reflection;
using Sinalo.App;
using Sinalo.Application.Synchronization;

namespace Sinalo.Tests.Unit;

public sealed class MainWindowSynchronizationStageTests
{
    [Theory]
    [InlineData("Baixando", SynchronizationStage.Download)]
    [InlineData("Extraindo vídeo", SynchronizationStage.Extraction)]
    [InlineData("Validando arquivo", SynchronizationStage.Validation)]
    [InlineData("Disponível offline", SynchronizationStage.Catalog)]
    [InlineData("Outra etapa", SynchronizationStage.Download)]
    public void GetSynchronizationStage_ShouldMapDownloadProgress(string stage, SynchronizationStage expected)
    {
        var method = typeof(MainWindow).GetMethod("GetSynchronizationStage", BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = (SynchronizationStage)method.Invoke(null, [stage])!;

        Assert.Equal(expected, result);
    }
}
