using System.IO;
using Microsoft.Data.Sqlite;
using Sinalo.App.ViewModels;
using Sinalo.Application.Library;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class CategoryInteractionTests
{
    [Fact] public async Task InitialSelectionUsesIdentityAndFreshRecordAndPreservesLaterChoice()
    {
        var service = new CategoryTestService();
        service.Rows[0] = service.Rows[0] with { Name = "Categoria renomeada", Revision = 4 };
        var vm = new CategoryManagerViewModel(service, "a");
        await vm.RefreshAsync();
        Assert.Equal(service.Rows[0], vm.Selected);
        Assert.Equal("Categoria renomeada", vm.Name);
        Assert.Contains(vm.Name, vm.Instruction);
        Assert.True(vm.CanDelete);
        vm.Selected = vm.Items[1]; vm.Name = "Rascunho";
        await vm.RefreshAsync();
        Assert.Equal("b", vm.Selected!.Id); Assert.Equal("Rascunho", vm.Name);
        vm.Selected = null;
        await vm.RefreshAsync(); Assert.Null(vm.Selected);
    }

    [Theory][InlineData(null)][InlineData("removed")]
    public async Task MissingOrAbsentInitialCategoryDoesNotSelectAnother(string? id)
    {
        var vm = new CategoryManagerViewModel(new CategoryTestService(), id);
        await vm.RefreshAsync();
        Assert.Null(vm.Selected); Assert.False(vm.CanDelete); Assert.Equal("", vm.Name);
    }

    [Fact] public async Task FailedInitialLoadRetainsContextForRetryIncludingPendingDeletion()
    {
        var service = new CategoryTestService { ListError = new IOException() };
        service.Rows[0] = service.Rows[0] with { IsDeleting = true };
        var vm = new CategoryManagerViewModel(service, "a");
        await vm.RefreshAsync(); Assert.Null(vm.Selected); Assert.False(vm.Busy);
        service.ListError = null;
        await vm.RefreshAsync();
        Assert.Equal("a", vm.Selected!.Id); Assert.Equal("A", vm.Name);
        Assert.True(vm.CanDelete); Assert.Contains("pendente", vm.Instruction);
    }

    [Fact] public async Task PreconditionsOrderingAndDraftSurviveValidation()
    {
        var service=new CategoryTestService();var vm=new CategoryManagerViewModel(service);
        Assert.False(vm.CanCreate);Assert.False(vm.CanDelete);Assert.False(vm.CanRename);
        Assert.False(vm.CanMoveUp);Assert.False(vm.CanMoveDown);Assert.Contains("Selecione",vm.Instruction);
        await vm.MoveUpAsync();Assert.Contains("Selecione",vm.Message);
        await vm.RenameAsync();await vm.RemoveAsync(null);Assert.Contains("Selecione",vm.Message);
        Assert.Null(await vm.PrepareRemovalAsync());
        await vm.RefreshAsync();vm.Name="Nova";Assert.True(vm.CreateCommand.CanExecute(null));
        await vm.CreateAsync();Assert.Equal("Nova",vm.Selected!.Name);Assert.True(vm.CanDelete);
        Assert.True(vm.CanMoveUp);Assert.False(vm.CanMoveDown);Assert.False(vm.CanRename);
        await vm.RenameAsync();Assert.Contains("não foi alterado",vm.Message);
        await vm.MoveDownAsync();Assert.Contains("limite",vm.Message);
        await vm.MoveUpAsync();Assert.Equal("Ordem atualizada.",vm.Message);
        vm.Name="A";await vm.RenameAsync();Assert.Equal("A",vm.Name);Assert.Contains("Já existe",vm.Message);Assert.Equal("Nova",vm.Selected.Name);
        vm.Name="Renomeada";Assert.True(vm.CanRename);await vm.RenameAsync();Assert.Equal("Renomeada",vm.Selected.Name);
        vm.Name="";await vm.CreateAsync();Assert.Contains("80",vm.Message);
        vm.Name="Renomeada";await vm.CreateAsync();Assert.Contains("Já existe",vm.Message);
        vm.Selected=vm.Items.First();Assert.False(vm.CanMoveUp);Assert.True(vm.CanMoveDown);
        vm.Selected=new("system","Sistema",IsSystem:true);Assert.False(vm.CanRename);Assert.False(vm.CanDelete);
        vm.Selected=new("pending","Pendente",IsDeleting:true);Assert.False(vm.CanMoveUp);Assert.False(vm.CanMoveDown);Assert.Contains("pendente",vm.Instruction);
        vm.Busy=true;Assert.False(vm.IsIdle);Assert.False(vm.CanCreate);Assert.Contains("Aguarde",vm.Instruction);
        await vm.RefreshAsync();await vm.CreateAsync();await vm.MoveDownAsync();Assert.Null(await vm.PrepareRemovalAsync());
    }
    [Fact] public async Task LoadFailureCanBeRetriedAndSuccessfulMutationIsNotReportedAsReversed()
    {
        var service=new CategoryTestService{ListError=new SqliteException("busy",5)};var vm=new CategoryManagerViewModel(service);
        await vm.RefreshAsync();Assert.Contains("catálogo",vm.Message);Assert.False(vm.Busy);
        service.ListError=null;await vm.ReloadCommand.ExecuteAsync(null);Assert.NotEmpty(vm.Items);
        vm.Name="Nova";service.FailListingAfterCreate=true;await vm.CreateAsync();
        Assert.Contains("Categoria criada",vm.Message);Assert.Contains("não pôde",vm.Message);Assert.False(vm.Busy);
        service.ListError=null;await vm.RefreshAsync();Assert.Contains(vm.Items,x=>x.Name=="Nova");
    }
    [Fact] public async Task CapturedRemovalDoesNotFollowLaterSelectionAndRejectsDuplicateExecution()
    {
        var service=new CategoryTestService();var vm=new CategoryManagerViewModel(service);await vm.RefreshAsync();
        vm.Selected=vm.Items[0];var original=vm.Selected.Id;
        var request=await vm.PrepareRemovalAsync();Assert.NotNull(request);Assert.True(vm.Busy);Assert.True(request!.CanContinue);
        Assert.Null(await vm.PrepareRemovalAsync());vm.Selected=vm.Items[1];
        service.RemoveWait=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var removal=vm.CompleteRemovalAsync(request);Assert.Equal(original,service.RemovedId);
        await vm.CompleteRemovalAsync(request);vm.CancelRemoval();Assert.True(vm.Busy);Assert.Equal(1,service.RemoveCalls);
        service.RemoveWait.SetResult();await removal;Assert.False(vm.Busy);Assert.DoesNotContain(vm.Items,x=>x.Id==original);
        Assert.Contains("Categoria excluída",vm.Message);await vm.CompleteRemovalAsync(request);Assert.Contains("Confira",vm.Message);
    }
    [Fact] public async Task CancellationInspectionFailureAndChangedRevisionReleaseState()
    {
        var service=new CategoryTestService();var vm=new CategoryManagerViewModel(service);await vm.RefreshAsync();vm.Selected=vm.Items[0];
        var request=await vm.PrepareRemovalAsync();vm.CancelRemoval();vm.CancelRemoval();Assert.Contains("cancelada",vm.Message);Assert.False(vm.Busy);
        await vm.CompleteRemovalAsync(request!);Assert.Contains("Confira",vm.Message);
        service.InspectError=new IOException("locked");Assert.Null(await vm.PrepareRemovalAsync());Assert.False(vm.Busy);Assert.Contains("arquivo",vm.Message);
        service.InspectError=null;service.ChangeRevisionOnInspect=true;Assert.Null(await vm.PrepareRemovalAsync());Assert.Contains("mudou",vm.Message);Assert.False(vm.Busy);
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task RemovalFailuresAndRefreshFailuresKeepAccurateResult(bool mutationFails)
    {
        var service=new CategoryTestService();var vm=new CategoryManagerViewModel(service);await vm.RefreshAsync();vm.Selected=vm.Items[0];
        var request=(await vm.PrepareRemovalAsync())!;
        service.RemoveError=mutationFails?new UnauthorizedAccessException():null;service.FailListingAfterRemove=true;
        await vm.CompleteRemovalAsync(request);Assert.False(vm.Busy);
        Assert.Contains(mutationFails?"Acesso negado":"Categoria excluída",vm.Message);Assert.Contains("Atualizar lista",vm.Message);
        if(!mutationFails)Assert.DoesNotContain(service.Rows,x=>x.Id==request.Category.Id);
    }
    [Fact] public async Task NonEmptyRemovalRequiresChoiceAndExplicitValidDestination()
    {
        var service=new CategoryTestService{Impact=new(1,1,1,4096,["a","b"])};
        var vm=new CategoryManagerViewModel(service);await vm.RefreshAsync();vm.Selected=vm.Items[0];
        var request=(await vm.PrepareRemovalAsync())!;Assert.True(request.ShowChoices);Assert.False(request.IsEmpty);Assert.False(request.CanContinue);
        await vm.CompleteRemovalAsync(request);Assert.Contains("Confira",vm.Message);
        request.IsTransfer=false;request.IsDelete=false;Assert.Equal(CategoryRemovalMode.None,request.Mode);
        request.IsTransfer=true;Assert.False(request.CanContinue);Assert.Contains("Escolha a categoria",request.Hint);
        request.Destination=new("absent","Ausente");Assert.False(request.CanContinue);
        request.Destination=request.Destinations[0];Assert.True(request.CanContinue);Assert.Contains("preserva",request.Hint);
        request.IsDelete=true;Assert.Contains("fixadas",request.Hint);Assert.Contains("Excluir mídias",request.ActionLabel);
        request.IsTransfer=true;await vm.CompleteRemovalAsync(request);Assert.Contains("transferidas",vm.Message);Assert.Equal(request.Destination.Id,service.TransferredTo);
    }
    [Fact] public void PendingAndEmptyDialogsHaveSafeDistinctStates()
    {
        var current=new MediaCategory("a","A");var pending=new MediaCategory("b","B",IsDeleting:true);
        var empty=new CategoryRemovalViewModel(current,new(0,0,0,0,[]),[current,pending,new("c","C")]);
        Assert.False(empty.ShowChoices);Assert.Equal("Excluir categoria",empty.ActionLabel);Assert.Contains("Apenas",empty.Summary);Assert.Contains("somente",empty.Hint);
        Assert.Single(empty.Destinations);Assert.Contains("A",empty.Title);
        var retry=new CategoryRemovalViewModel(pending,new(1,0,1,1,["x"]),[current,pending]);
        Assert.False(retry.ShowChoices);Assert.True(retry.CanContinue);Assert.Contains("Retomar",retry.Title);Assert.Equal("Retomar exclusão",retry.ActionLabel);Assert.Contains("pendente",retry.Hint);
        retry.IsTransfer=true;retry.Destination=current;Assert.False(retry.CanContinue);
    }
    [Theory]
    [InlineData(0,"Acesso negado")][InlineData(1,"catálogo")][InlineData(2,"arquivo")]
    [InlineData(3,"inválido")][InlineData(4,"alterado")][InlineData(5,"interrompida")][InlineData(6,"inesperada")]
    public void FailureMessagesHaveRecoveryGuidance(int kind,string expected)
    {
        Exception error=kind switch{0=>new UnauthorizedAccessException(),1=>new SqliteException("db",5),2=>new IOException(),3=>new ArgumentException("inválido"),4=>new InvalidOperationException("alterado"),5=>new OperationCanceledException(),_=>new Exception("secret")};
        Assert.Contains(expected,CategoryManagerViewModel.Describe(error));Assert.DoesNotContain("secret",CategoryManagerViewModel.Describe(error));
    }
}

internal sealed class CategoryTestService:IMediaCategoryService
{
    public event Action? Changed;
    public event Action<IReadOnlyList<string>>? MediaRemoved {add{}remove{}}
    public List<MediaCategory> Rows {get;}=[new("a","A"),new("b","B",1)];
    public Exception? ListError,InspectError,RemoveError;
    public bool FailListingAfterCreate,FailListingAfterRemove,ChangeRevisionOnInspect;
    public int RemoveCalls;public string? RemovedId,TransferredTo;
    public TaskCompletionSource? RemoveWait;
    public CategoryImpact Impact=new(0,0,0,0,[]);
    public Task<IReadOnlyList<MediaCategory>> ListAsync(CancellationToken token=default)=>ListError is {} error?Task.FromException<IReadOnlyList<MediaCategory>>(error):Task.FromResult<IReadOnlyList<MediaCategory>>(Rows.ToArray());
    public Task<MediaCategory> CreateAsync(string name,CancellationToken token=default)
    {
        var label=MediaCategory.ValidateName(name);if(Rows.Any(x=>x.Name==label))throw new ArgumentException("Já existe uma categoria com esse nome.");
        var row=new MediaCategory(Guid.NewGuid().ToString(),label,Rows.Count);Rows.Add(row);
        if(FailListingAfterCreate)ListError=new IOException();Changed?.Invoke();return Task.FromResult(row);
    }
    public Task RenameAsync(MediaCategory category,string name,CancellationToken token=default)
    {
        if(Rows.Any(x=>x.Name==name&&x.Id!=category.Id))throw new ArgumentException("Já existe uma categoria com esse nome.");
        Rows[Rows.FindIndex(x=>x.Id==category.Id)]=category with{Name=name,Revision=category.Revision+1};Changed?.Invoke();return Task.CompletedTask;
    }
    public Task ReorderAsync(string id,int direction,CancellationToken token=default)
    {var i=Rows.FindIndex(x=>x.Id==id);(Rows[i],Rows[i+direction])=(Rows[i+direction],Rows[i]);return Task.CompletedTask;}
    public Task<CategoryImpact> InspectAsync(string categoryId,CancellationToken token=default)
    {if(InspectError is {}e)return Task.FromException<CategoryImpact>(e);if(ChangeRevisionOnInspect)Rows[0]=Rows[0] with{Revision=1};return Task.FromResult(Impact);}
    public async Task RemoveAsync(MediaCategory category,string? transferTo,CancellationToken token=default,IReadOnlyList<string>? expectedMediaIds=null)
    {
        RemoveCalls++;RemovedId=category.Id;TransferredTo=transferTo;
        if(RemoveWait is {}wait)await wait.Task;
        if(FailListingAfterRemove)ListError=new IOException();if(RemoveError is {}e)throw e;
        Rows.RemoveAll(x=>x.Id==category.Id);Changed?.Invoke();
    }
    public Task AssignAsync(IReadOnlyList<string> mediaIds,string categoryId,CancellationToken token=default)=>Task.CompletedTask;
    public Task ValidateDestinationAsync(string categoryId,CancellationToken token=default)=>Task.CompletedTask;
    public Task<LibraryMedia?> FindLinkedAsync(string videoId,string categoryId,CancellationToken token=default)=>Task.FromResult<LibraryMedia?>(null);
    public Task PublishLinkedAsync(Sinalo.Application.Synchronization.LinkedVideoDownloadRequest request,ContentItem downloaded,CancellationToken token=default)=>Task.CompletedTask;
}
