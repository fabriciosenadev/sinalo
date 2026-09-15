using System.Net;
using Sinalo.Application.Storage;
using Sinalo.Domain;

namespace Sinalo.Application.Synchronization;

public enum SynchronizationFailureCategory
{
    NoConnection,
    ServiceUnavailable,
    AccessDenied,
    SourceChanged,
    InsufficientStorage,
    InvalidFile,
    LocalFailure
}

public enum SynchronizationStage
{
    Discovery,
    Download,
    Validation,
    Extraction,
    Storage,
    Catalog
}

public sealed record SynchronizationDiagnostic(
    ContentSource Source,
    string SourceName,
    SynchronizationStage Stage,
    SynchronizationFailureCategory Category,
    string FriendlyMessage,
    string RecommendedAction,
    DateTimeOffset OccurredAt,
    string? SourceUrl,
    int? HttpStatusCode,
    string TechnicalDetails)
{
    public string SupportText => $"Programa: {SourceName}\nEtapa: {SynchronizationFailureClassifier.GetStageLabel(Stage)}\nOcorrido em: {OccurredAt.LocalDateTime:dd/MM/yyyy HH:mm}\nSituação: {FriendlyMessage}\nAção recomendada: {RecommendedAction}\nOrigem: {SourceUrl ?? "não informada"}\nHTTP: {(HttpStatusCode?.ToString() ?? "não informado")}\nDetalhes: {TechnicalDetails}";
}

public sealed class SiteStructureChangedException(string message) : Exception(message);

public interface ISynchronizationDiagnosticStore
{
    Task RecordAsync(SynchronizationDiagnostic diagnostic, CancellationToken cancellationToken = default);
}

public static class SynchronizationFailureClassifier
{
    public static SynchronizationDiagnostic Classify(
        Exception exception,
        ContentSource source,
        string sourceName,
        string? sourceUrl,
        SynchronizationStage stage)
    {
        var http = exception as HttpRequestException;
        var category = exception switch
        {
            InsufficientStorageSpaceException or StorageSpaceCriticalException => SynchronizationFailureCategory.InsufficientStorage,
            SiteStructureChangedException or UriFormatException => SynchronizationFailureCategory.SourceChanged,
            InvalidDataException => SynchronizationFailureCategory.InvalidFile,
            UnauthorizedAccessException or IOException => SynchronizationFailureCategory.LocalFailure,
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => SynchronizationFailureCategory.AccessDenied,
            HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone } => SynchronizationFailureCategory.SourceChanged,
            HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } or TaskCanceledException => SynchronizationFailureCategory.ServiceUnavailable,
            HttpRequestException => SynchronizationFailureCategory.NoConnection,
            _ => SynchronizationFailureCategory.LocalFailure
        };

        var (message, action) = category switch
        {
            SynchronizationFailureCategory.NoConnection => ("Não foi possível conectar à internet.", "Verifique a conexão e tente novamente."),
            SynchronizationFailureCategory.ServiceUnavailable => ("O site do programa não respondeu agora.", "Tente novamente mais tarde."),
            SynchronizationFailureCategory.AccessDenied => ("O arquivo não pôde ser acessado no site oficial.", "Confira a configuração e informe o suporte se persistir."),
            SynchronizationFailureCategory.SourceChanged => ("A estrutura do site do programa parece ter mudado.", "Confira a página oficial configurada; não a altere se ela estiver correta."),
            SynchronizationFailureCategory.InsufficientStorage => ("Não há espaço suficiente na pasta de conteúdo.", "Libere espaço ou escolha outra pasta nas configurações."),
            SynchronizationFailureCategory.InvalidFile => ("O arquivo baixado não pôde ser validado.", "Tente novamente; se persistir, informe o suporte."),
            _ => ("O Sinalo não conseguiu salvar ou organizar o vídeo.", "Confira a pasta configurada, permissões e programas usando o arquivo.")
        };

        return new SynchronizationDiagnostic(
            source, sourceName, stage, category, message, action, DateTimeOffset.Now,
            SanitizeUrl(sourceUrl), http?.StatusCode is { } status ? (int)status : null,
            SanitizeTechnicalDetails(exception));
    }

    public static string GetStageLabel(SynchronizationStage stage) => stage switch
    {
        SynchronizationStage.Discovery => "descoberta do conteúdo",
        SynchronizationStage.Download => "download",
        SynchronizationStage.Validation => "validação do arquivo",
        SynchronizationStage.Extraction => "extração do arquivo",
        SynchronizationStage.Storage => "gravação local",
        SynchronizationStage.Catalog => "atualização do catálogo",
        _ => "sincronização"
    };

    public static string? SanitizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.GetLeftPart(UriPartial.Path);
    }

    public static string SanitizeTechnicalDetails(Exception exception)
    {
        var text = $"{exception.GetType().Name}: {exception.Message}";
        text = System.Text.RegularExpressions.Regex.Replace(text, "(?i)(token|authorization|cookie|password)=?[^\\s,;]+", "$1=[removido]");
        return text.Length <= 1000 ? text : text[..1000];
    }
}
