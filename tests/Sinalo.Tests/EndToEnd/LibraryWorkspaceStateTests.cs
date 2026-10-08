using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Domain;

namespace Sinalo.Tests.EndToEnd;

public sealed class LibraryWorkspaceStateTests
{
    private static LibraryViewModel Model(LibraryPreviewRepository repository, ILibraryImportService? importer = null) => new(repository,importer!,null!,null!,null!,null!);
    [Theory]
    [InlineData(LibraryEditDecision.Cancel,false)]
    [InlineData(LibraryEditDecision.Discard,true)]
    [InlineData(LibraryEditDecision.Save,true)]
    public async Task SelectingAnotherVideoProtectsPendingEdits(LibraryEditDecision decision,bool changed)
    {
        var repository=new LibraryPreviewRepository(); var model=Model(repository); await model.RefreshAsync();
        model.Selected=model.Items[3]; var original=model.Selected;
        model.EditInformationCommand.Execute(null); model.EditName="Nome editado"; model.EditDate="10/10/2026";
        model.ConfirmPendingChanges=()=>decision;
        var next=model.Items[1]; Assert.Equal(changed,await model.SelectVideoAsync(next));
        Assert.Equal(changed ? next.Id:original.Id,model.Selected!.Id);
        Assert.Equal(decision==LibraryEditDecision.Save ? "Nome editado":original.Name,(await repository.FindAsync(original.Id))!.Name);
        Assert.Equal(!changed,model.HasPendingChanges);
    }
    [Fact]
    public async Task RefreshKeepsEditingBufferAndFailedSaveDoesNotAllowNavigation()
    {
        var repository=new LibraryPreviewRepository(); var model=Model(repository); await model.RefreshAsync();
        model.Selected=model.Items[3]; model.IsWideLayout=true; model.DetailsOpen=true;
        model.EditInformationCommand.Execute(null); model.EditName="Rascunho"; model.EditDate="99/99/2026";
        await model.RefreshAsync(); Assert.Equal("Rascunho",model.EditName); Assert.True(model.HasPendingChanges);
        model.ConfirmPendingChanges=()=>LibraryEditDecision.Save;
        Assert.False(await model.SelectVideoAsync(model.Items[1])); Assert.Contains("DD/MM",model.ActionMessage);
        model.ConfirmPendingChanges=()=>LibraryEditDecision.Cancel; await model.CloseDetailsCommand.ExecuteAsync(null); Assert.True(model.DetailsOpen);
        model.ConfirmPendingChanges=()=>LibraryEditDecision.Discard; await model.CloseDetailsCommand.ExecuteAsync(null);
        Assert.False(model.DetailsOpen); Assert.False(model.IsEditing); Assert.Equal(model.Selected!.Name,model.EditName);
        model.EditInformationCommand.Execute(null); model.EditName="Outro rascunho"; model.CancelEditingCommand.Execute(null); Assert.False(model.HasPendingChanges);
    }
    [Fact]
    public async Task ContextualViewsAndLabelsMatchMediaAndOutputWithoutLosingState()
    {
        var repository=new LibraryPreviewRepository(); var model=Model(repository);
        Assert.True(await model.ResolvePendingChangesAsync()); Assert.True(model.ShowEmpty); Assert.False(model.HasSelection);
        Assert.Contains("vazia",model.EmptyTitle); model.EditInformationCommand.Execute(null); model.ShowDetailsPanelCommand.Execute(null);
        await model.RefreshAsync(); Assert.Contains("40",model.CountLabel); Assert.False(model.ShowEmpty);
        model.IsWideLayout=true; Assert.True(model.ShowColumnHeaders); Assert.True(model.ShowFilters);
        await model.SelectVideoAsync(model.Items[1]); Assert.True(model.ShowDetails); Assert.True(model.ShowList); Assert.True(model.IsProgramVideo);
        Assert.Contains("Excluir",model.RemoveLabel); Assert.Contains("novamente",model.RemovalExplanation); Assert.Equal("Fixar vídeo",model.PinLabel);
        model.OutputScreenLabel="Tela 2"; Assert.Equal("Reproduzir na Tela 2",model.PlayLabel); Assert.True(model.CanPlaySelected);
        model.PresentationIsOpen=true; Assert.False(model.CanPlaySelected); model.PresentationIsOpen=false;
        model.HasOutputScreen=false; Assert.False(model.CanPlaySelected); Assert.Equal("Nenhuma tela de saída",model.PlayLabel); model.HasOutputScreen=true;
        await model.SelectVideoAsync(model.Items[0]); Assert.True(model.NeedsLocate); Assert.False(model.CanPlaySelected); Assert.Equal("Desafixar vídeo",model.PinLabel);
        await model.SelectVideoAsync(model.Items[3]); Assert.True(model.IsImported); Assert.False(model.IsProgramVideo); Assert.False(model.CanDeleteCopy);
        Assert.Equal("Sem data de uso",model.UsageDateLabel); Assert.Contains("original",model.RemovalExplanation); Assert.Equal("Remover da biblioteca",model.RemoveLabel);
        model.Selected=model.Selected! with { StorageMode=MediaStorageMode.Managed }; Assert.True(model.CanDeleteCopy);
        model.IsWideLayout=false; model.DetailsOpen=true; Assert.False(model.ShowList); Assert.True(model.ShowCompactRows);
        model.DetailsOpen=false; Assert.True(model.ShowList); model.ToggleFiltersCommand.Execute(null); Assert.True(model.ShowFilters);
        model.CopyIntoSinalo=false; Assert.True(model.UseOriginal); model.CopyIntoSinalo=true; Assert.False(model.UseOriginal);
        model.ShowImportsCommand.Execute(null); Assert.False(model.ShowList); Assert.False(model.ShowDetails); model.ShowVideoListCommand.Execute(null); Assert.True(model.ShowList);
        model.Selected=null; Assert.False(model.DetailsOpen); Assert.False(model.HasSelection); Assert.True(model.ShowReading);
        Assert.Equal("0 reprodução(ões)",model.PlaybackCountLabel);
    }
    [Fact]
    public async Task PaginationEmptySearchAndClearingFiltersHaveCoherentFeedback()
    {
        var repository=new LibraryPreviewRepository(); repository.Media.AddRange(repository.Media.Select(x=>x with { Id=x.Id+"copy" }).ToArray());
        var model=Model(repository); await model.RefreshAsync(); Assert.True(model.CanNext); Assert.False(model.CanPrevious);
        await model.NextPageCommand.ExecuteAsync(null); Assert.True(model.CanPrevious); Assert.False(model.CanNext); Assert.Equal(30,model.Items.Count);
        await model.PreviousPageCommand.ExecuteAsync(null); Assert.False(model.CanPrevious); Assert.Equal(50,model.Items.Count);
        model.Search="texto inexistente"; await model.SearchNowCommand.ExecuteAsync(null); Assert.True(model.ShowEmpty); Assert.Contains("pesquisa",model.EmptyTitle);
        Assert.Contains("filtros",model.EmptyDescription); Assert.Contains("resultado",model.CountLabel);
        await model.ClearFiltersCommand.ExecuteAsync(null); Assert.False(model.ShowEmpty); Assert.Equal("",model.Search); Assert.Equal("Todos",model.Origin.Value);
        await model.SelectVideoAsync(model.Items[1]); Assert.True(await model.SelectVideoAsync(model.Items[1]));
        model.EditInformationCommand.Execute(null); model.EditName="Não perder"; Assert.False(await model.SelectVideoAsync(null));
        model.Search="outro"; await model.RefreshAsync(); Assert.NotEmpty(model.Items); Assert.Equal("Não perder",model.EditName);
    }
    [Fact]
    public async Task ImportCanContinueAcrossTabsAndKeepsItsResultSeparateFromActions()
    {
        var importer=new ControlledImport(); var model=Model(new LibraryPreviewRepository(),importer);
        model.ShowImportsCommand.Execute(null); var running=model.ImportAsync(["file.mp4"]); await importer.Started.Task;
        Assert.True(model.IsImporting); Assert.False(model.CanImport); model.ShowVideoListCommand.Execute(null); Assert.False(model.IsImportTab);
        await model.ImportAsync(["second.mp4"]); Assert.Equal(1,importer.Calls);
        model.Message="Resultado de reprodução"; Assert.Equal("Resultado de reprodução",model.ActionMessage);
        importer.Finish.TrySetResult(new(2,3,4,["Um arquivo inválido"],false)); await running; await model.WhenImportIdleAsync();
        Assert.True(model.CanImport); Assert.True(model.HasImportResult); Assert.Equal(2,model.ImportedCount); Assert.Equal(3,model.ExistingCount);
        Assert.True(model.HasImportFailures);
        Assert.Equal(4,model.IgnoredCount); Assert.Equal(1,model.FailedCount); Assert.Contains("inválido",model.ImportFailures); Assert.Contains("concluída",model.ImportMessage);
        model.Message="Outra ação"; Assert.Contains("concluída",model.ImportMessage); Assert.Equal("Outra ação",model.ActionMessage);
    }
    private sealed class ControlledImport : ILibraryImportService
    {
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<LibraryImportResult> Finish {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls {get;private set;}
        public Task<LibraryImportResult> ImportAsync(LibraryImportRequest request,IProgress<LibraryImportProgress>? progress=null,CancellationToken token=default) { Calls++; Started.TrySetResult(); return Finish.Task; }
        public Task RecoverAsync(CancellationToken token=default)=>Task.CompletedTask;
    }
}
