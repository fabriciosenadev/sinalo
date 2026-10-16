using CommunityToolkit.Mvvm.ComponentModel;
using Sinalo.Application.Library;
using Sinalo.Domain;

namespace Sinalo.App.ViewModels;

public enum CategoryRemovalMode { None, Transfer, Delete }
public sealed partial class CategoryRemovalViewModel : ObservableObject
{
    public MediaCategory Category { get; }
    public CategoryImpact Impact { get; }
    public IReadOnlyList<MediaCategory> Destinations { get; }
    [ObservableProperty] private CategoryRemovalMode mode;
    [ObservableProperty] private MediaCategory? destination;
    public CategoryRemovalViewModel(MediaCategory category, CategoryImpact impact, IReadOnlyList<MediaCategory> all)
    {
        Category = category; Impact = impact;
        Destinations = all.Where(x => x.Id != category.Id && !x.IsDeleting).ToArray();
        Mode = category.IsDeleting || IsEmpty ? CategoryRemovalMode.Delete : CategoryRemovalMode.None;
    }
    public bool IsEmpty => Impact.MediaIds.Count == 0;
    public bool ShowChoices => !IsEmpty && !Category.IsDeleting;
    public bool IsTransfer { get => Mode == CategoryRemovalMode.Transfer; set { if (value) Mode = CategoryRemovalMode.Transfer; } }
    public bool IsDelete { get => Mode == CategoryRemovalMode.Delete; set { if (value) Mode = CategoryRemovalMode.Delete; } }
    public bool CanContinue => IsDelete || (IsTransfer && !Category.IsDeleting && Destination is { } target && Destinations.Any(x => x.Id == target.Id));
    public string Title => Category.IsDeleting ? $"Retomar exclusão de “{Category.Name}”?" : $"Excluir “{Category.Name}”?";
    public string Summary => IsEmpty ? "A categoria não tem mídias. Apenas a categoria será removida."
        : $"{Impact.ManagedCopies} cópia(s) gerenciada(s), {Impact.References} referência(s) externa(s), {Impact.Pinned} fixado(s). {Impact.Bytes / 1024 / 1024} MB conhecidos.";
    public string Hint => Category.IsDeleting ? "A exclusão anterior ficou pendente. Confira os arquivos e retome explicitamente; não é possível transferir uma exclusão já iniciada."
        : IsEmpty ? "Confirme para excluir somente esta categoria."
        : Mode == CategoryRemovalMode.None ? "Escolha o que fazer com as mídias antes de continuar."
        : IsTransfer ? Destination is null ? "Escolha a categoria que receberá todas as mídias e referências." : "A transferência preserva arquivos, histórico, fixação e referências."
        : "As cópias gerenciadas serão apagadas, inclusive as fixadas. Os vínculos internos serão removidos. Originais externos nunca serão apagados.";
    public string ActionLabel => Category.IsDeleting ? "Retomar exclusão" : IsTransfer ? "Transferir e excluir categoria" : IsEmpty ? "Excluir categoria" : IsDelete ? "Excluir mídias e categoria" : "Escolha uma operação";
    partial void OnModeChanged(CategoryRemovalMode value) => Notify();
    partial void OnDestinationChanged(MediaCategory? value) => Notify();
    private void Notify()
    {
        foreach (var name in new[] { nameof(IsTransfer), nameof(IsDelete), nameof(CanContinue), nameof(Hint), nameof(ActionLabel) }) OnPropertyChanged(name);
    }
}
