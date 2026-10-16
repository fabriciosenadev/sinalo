using System.Text;

namespace Sinalo.Domain;

public sealed record MediaCategory(string Id, string Name, int Position = 0, bool IsSystem = false, long Revision = 0, bool IsDeleting = false)
{
    public const string Uncategorized = "uncategorized";
    public static string ForSource(ContentSource source) => source switch
    { ContentSource.Missions => "missions", ContentSource.ProvaiEVede => "provai-e-vede", ContentSource.Health => "health", _ => throw new ArgumentOutOfRangeException(nameof(source)) };
    public static ContentSource? SourceFor(string id) => id switch
    { "missions" => ContentSource.Missions, "provai-e-vede" => ContentSource.ProvaiEVede, "health" => ContentSource.Health, _ => null };
    public static string ValidateName(string name)
    {
        var value = name.Trim().Normalize(NormalizationForm.FormC);
        if (value.Length is < 1 or > 80 || value.Any(char.IsControl)) throw new ArgumentException("Informe um nome de 1 a 80 caracteres, sem caracteres de controle.");
        return value;
    }
    public static string NameKey(string name) => ValidateName(name).ToUpperInvariant();
    public override string ToString() => Name + (IsDeleting ? " (exclusão pendente)" : "");
}
