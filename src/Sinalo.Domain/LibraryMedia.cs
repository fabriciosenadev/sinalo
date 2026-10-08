namespace Sinalo.Domain;

public enum LibraryMediaType { Video }
public enum MediaStorageMode { Managed, Referenced }
public enum MediaAvailability { Available, Missing, Invalid }
public sealed record LibraryMedia(string Id, string Name, string? ContentItemId, ContentSource? Source,
    DateOnly? UsageDate, string? LocalPath, string? OriginalPath, MediaStorageMode StorageMode,
    MediaAvailability Availability, DateTimeOffset AddedAtUtc, long? SizeBytes = null, string? Sha256 = null,
    int PlayCount = 0, bool IsPinned = false, LibraryMediaType Type = LibraryMediaType.Video,
    DateTimeOffset? FirstPlayedAtUtc = null, DateTimeOffset? LastPlayedAtUtc = null)
{
    public bool IsImported => ContentItemId is null;
    public string OriginLabel => Source switch { ContentSource.Missions => "Informativo das Missões", ContentSource.ProvaiEVede => "Provai e Vede", ContentSource.Health => "Minuto de Saúde", _ => "Importado" };
    public string StorageLabel => StorageMode == MediaStorageMode.Managed ? "Cópia na pasta do Sinalo" : "Arquivo no local original";
    public bool CanPlay => Availability == MediaAvailability.Available && !string.IsNullOrWhiteSpace(LocalPath);
    public string AvailabilityLabel => Availability switch { MediaAvailability.Available => "Pronto para reproduzir", MediaAvailability.Missing => "Arquivo ausente", _ => "Arquivo inválido" };
}
