using System.Collections.ObjectModel;
using System.Data.Common;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinalo.Application.Library;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public sealed partial class CategoryManagerViewModel(IMediaCategoryService service, string? initialCategoryId = null) : ObservableObject
{
    private string? _initialCategoryId = initialCategoryId;
    private bool _reloading;
    private CategoryRemovalViewModel? _removal;
    private bool _removing;
    public ObservableCollection<MediaCategory> Items { get; } = [];
    [ObservableProperty] private MediaCategory? selected;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string message = "Digite um nome para criar ou selecione uma categoria para gerenciar.";
    [ObservableProperty] private bool busy;
    public bool IsIdle => !Busy;
    public bool CanCreate => !Busy && ValidName;
    public bool CanRename => !Busy && Selected is { IsSystem: false, IsDeleting: false } && ValidName && Name.Trim() != Selected.Name;
    public bool CanDelete => !Busy && Selected is { IsSystem: false };
    private List<MediaCategory> Ordered => Items.Where(x => !x.IsDeleting).ToList();
    public bool CanMoveUp => !Busy && Selected is { IsDeleting: false } && Ordered.FindIndex(x => x.Id == Selected.Id) > 0;
    public bool CanMoveDown => !Busy && Selected is { IsDeleting: false } && Ordered.FindIndex(x => x.Id == Selected.Id) is var i && i >= 0 && i < Ordered.Count - 1;
    public string Instruction => Busy ? "Aguarde a operação atual. Nenhuma nova alteração será iniciada."
        : Selected is null ? "Selecione uma categoria para renomear, ordenar ou excluir."
        : Selected.IsDeleting ? "Exclusão pendente: selecione Excluir categoria para conferir e retomar."
        : $"Categoria selecionada: {Selected.Name}. Você pode renomear, ordenar ou excluir.";
    private bool ValidName { get { try { MediaCategory.ValidateName(Name); return true; } catch (ArgumentException) { return false; } } }
    partial void OnSelectedChanged(MediaCategory? value) { if (!_reloading) Name = value?.Name ?? ""; NotifyActions(); }
    partial void OnNameChanged(string value) => NotifyActions();
    partial void OnBusyChanged(bool value) { OnPropertyChanged(nameof(IsIdle)); NotifyActions(); }
    private void NotifyActions()
    {
        foreach (var property in new[] { nameof(CanCreate), nameof(CanRename), nameof(CanDelete), nameof(CanMoveUp), nameof(CanMoveDown), nameof(Instruction) }) OnPropertyChanged(property);
        CreateCommand.NotifyCanExecuteChanged(); RenameCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged(); MoveDownCommand.NotifyCanExecuteChanged(); ReloadCommand.NotifyCanExecuteChanged();
    }
    private async Task ReloadCoreAsync(string? preferred, bool preserveDraft)
    {
        var list = await Task.Run(() => service.ListAsync());
        var draft = Name;
        _reloading = true;
        try
        {
            Items.Clear(); foreach (var item in list.Where(x => !x.IsSystem)) Items.Add(item);
            Selected = Items.FirstOrDefault(x => x.Id == preferred);
        }
        finally { _reloading = false; }
        Name = preserveDraft ? draft : Selected?.Name ?? "";
        NotifyActions();
    }
    public async Task RefreshAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            await ReloadCoreAsync(Selected?.Id ?? _initialCategoryId, _initialCategoryId is null);
            _initialCategoryId = null;
            Message = "Lista atualizada. " + (Selected is null ? "Selecione uma categoria ou crie uma nova." : $"Categoria selecionada: {Selected.Name}.");
        }
        catch (Exception error) { Message = "Não foi possível carregar as categorias. " + Describe(error); }
        finally { Busy = false; }
    }
    [RelayCommand(CanExecute = nameof(IsIdle))] private Task ReloadAsync() => RefreshAsync();
    private async Task RunAsync(Func<Task<string?>> action, string success)
    {
        if (Busy) return;
        Busy = true; Message = "Processando a alteração. Aguarde…";
        try
        {
            var id = await action();
            try { await ReloadCoreAsync(id, false); Message = success; }
            catch (Exception error) { Message = success + " A lista não pôde ser atualizada. Use Atualizar lista. " + Describe(error); }
        }
        catch (Exception error) { Message = Describe(error); }
        finally { Busy = false; }
    }
    [RelayCommand(CanExecute = nameof(CanCreate))]
    public Task CreateAsync() => RunAsync(async () => (await service.CreateAsync(Name)).Id, "Categoria criada.");
    [RelayCommand(CanExecute = nameof(CanRename))]
    public Task RenameAsync()
    {
        var category = Selected;
        if (category is null) { Message = "Selecione uma categoria."; return Task.CompletedTask; }
        if (Name.Trim() == category.Name) { Message = "O nome não foi alterado."; return Task.CompletedTask; }
        return RunAsync(async () => { await service.RenameAsync(category, Name); return category.Id; }, "Categoria renomeada.");
    }
    [RelayCommand(CanExecute = nameof(CanMoveUp))] public Task MoveUpAsync() => MoveAsync(-1);
    [RelayCommand(CanExecute = nameof(CanMoveDown))] public Task MoveDownAsync() => MoveAsync(1);
    private Task MoveAsync(int direction)
    {
        if (!(direction < 0 ? CanMoveUp : CanMoveDown)) { if (!Busy) Message = Selected is null ? "Selecione uma categoria." : "A categoria já está no limite da ordenação."; return Task.CompletedTask; }
        var category = Selected!;
        return RunAsync(async () => { await service.ReorderAsync(category.Id, direction); return category.Id; }, "Ordem atualizada.");
    }
    public async Task<CategoryRemovalViewModel?> PrepareRemovalAsync()
    {
        if (!CanDelete) { if (!Busy) Message = "Selecione uma categoria personalizada para excluir."; return null; }
        var category = Selected!;
        Busy = true; Message = "Conferindo as mídias da categoria…";
        try
        {
            var impact = await service.InspectAsync(category.Id);
            var list = await Task.Run(() => service.ListAsync());
            var current = list.SingleOrDefault(x => x.Id == category.Id);
            if (current != category) throw new InvalidOperationException("A categoria mudou. Atualize a lista e confira novamente.");
            return _removal = new(category, impact, list);
        }
        catch (Exception error) { Message = Describe(error); Busy = false; return null; }
    }
    public void CancelRemoval()
    {
        if (_removal is null || _removing) return;
        _removal = null; Busy = false; Message = "Operação cancelada. Nenhuma exclusão foi iniciada.";
    }
    public async Task CompleteRemovalAsync(CategoryRemovalViewModel request)
    {
        if (_removing) return;
        if (!ReferenceEquals(request, _removal) || !request.CanContinue) { Message = "Confira a operação e a categoria de destino antes de continuar."; return; }
        _removing = true;
        bool completed = false;
        Message = "Processando a categoria. Aguarde…";
        try
        {
            await service.RemoveAsync(request.Category, request.IsTransfer ? request.Destination!.Id : null, expectedMediaIds: request.Impact.MediaIds);
            completed = true;
            Message = request.IsTransfer ? "Mídias e referências transferidas. Categoria excluída; arquivos preservados." : "Categoria excluída. Originais externos preservados.";
        }
        catch (Exception error) { Message = "Não foi possível concluir. " + Describe(error) + " Confira se a categoria ficou com exclusão pendente antes de tentar novamente."; }
        finally
        {
            var result = Message;
            try { await ReloadCoreAsync(completed ? null : request.Category.Id, false); Message = result; }
            catch (Exception error) { Message = result + " Não foi possível atualizar a lista. Use Atualizar lista. " + Describe(error); }
            _removal = null; _removing = false; Busy = false;
        }
    }
    // Existing callers also capture identity before awaiting.
    public Task RemoveAsync(string? target, IReadOnlyList<string>? expected = null)
    {
        var category = Selected;
        if (category is null) { Message = "Selecione uma categoria."; return Task.CompletedTask; }
        return RunAsync(async () => { await service.RemoveAsync(category, target, expectedMediaIds: expected); return null; }, "Categoria excluída.");
    }
    public static string Describe(Exception error) => error switch
    {
        UnauthorizedAccessException => "Acesso negado. Verifique as permissões da pasta e tente novamente.",
        DbException => "O catálogo local está indisponível ou ocupado. Aguarde e use Atualizar lista; se persistir, reinicie o Sinalo.",
        IOException => "Não foi possível acessar um arquivo ou ele mudou desde a confirmação. Feche programas que o utilizam e confira os arquivos antes de retomar.",
        ArgumentException or InvalidOperationException => error.Message,
        OperationCanceledException => "Operação interrompida. Confira o estado da categoria antes de retomar.",
        _ => "Ocorreu uma falha inesperada. Atualize a lista e tente novamente."
    };
}
