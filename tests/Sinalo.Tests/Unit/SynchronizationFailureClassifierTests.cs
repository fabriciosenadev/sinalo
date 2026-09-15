using System.Net;
using System.Net.Http;
using System.IO;
using Sinalo.Application.Storage;
using Sinalo.Application.Synchronization;
using Sinalo.Domain;

namespace Sinalo.Tests.Unit;

public sealed class SynchronizationFailureClassifierTests
{
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, SynchronizationFailureCategory.AccessDenied)]
    [InlineData(HttpStatusCode.NotFound, SynchronizationFailureCategory.SourceChanged)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SynchronizationFailureCategory.ServiceUnavailable)]
    public void Classify_ShouldMapHttpResponsesToFriendlyCategories(HttpStatusCode status, SynchronizationFailureCategory expected)
    {
        var diagnostic = SynchronizationFailureClassifier.Classify(
            new HttpRequestException("Falhou", null, status), ContentSource.Missions, "Informativo das Missões",
            "https://user:secret@example.test/path?token=abc", SynchronizationStage.Discovery);

        Assert.Equal(expected, diagnostic.Category);
        Assert.Equal("https://example.test/path", diagnostic.SourceUrl);
        Assert.DoesNotContain("secret", diagnostic.SupportText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=abc", diagnostic.SupportText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classify_ShouldMapStorageAndInvalidFiles()
    {
        var storage = SynchronizationFailureClassifier.Classify(
            new StorageSpaceCriticalException(), ContentSource.Health, "Minuto de Saúde", "https://example.test", SynchronizationStage.Download);
        var file = SynchronizationFailureClassifier.Classify(
            new InvalidDataException("Arquivo vazio"), ContentSource.Health, "Minuto de Saúde", "https://example.test", SynchronizationStage.Validation);

        Assert.Equal(SynchronizationFailureCategory.InsufficientStorage, storage.Category);
        Assert.Equal(SynchronizationFailureCategory.InvalidFile, file.Category);
    }

    [Theory]
    [InlineData(SynchronizationFailureCategory.NoConnection)]
    [InlineData(SynchronizationFailureCategory.ServiceUnavailable)]
    [InlineData(SynchronizationFailureCategory.SourceChanged)]
    [InlineData(SynchronizationFailureCategory.LocalFailure)]
    public void Classify_ShouldExplainEveryRemainingOperatorCategory(SynchronizationFailureCategory expected)
    {
        Exception exception = expected switch
        {
            SynchronizationFailureCategory.NoConnection => new HttpRequestException("Sem rede"),
            SynchronizationFailureCategory.ServiceUnavailable => new TaskCanceledException("Tempo esgotado"),
            SynchronizationFailureCategory.SourceChanged => new SiteStructureChangedException("Página trimestral ausente"),
            _ => new UnauthorizedAccessException("Pasta bloqueada")
        };

        var diagnostic = SynchronizationFailureClassifier.Classify(
            exception, ContentSource.ProvaiEVede, "Provai e Vede", "endereço inválido", SynchronizationStage.Storage);

        Assert.Equal(expected, diagnostic.Category);
        Assert.Null(diagnostic.SourceUrl);
        Assert.NotEmpty(diagnostic.FriendlyMessage);
        Assert.NotEmpty(diagnostic.RecommendedAction);
    }

    [Fact]
    public void Classify_ShouldSanitizeSensitiveTechnicalDetails()
    {
        var diagnostic = SynchronizationFailureClassifier.Classify(
            new InvalidOperationException("authorization=Bearer-secret token=abc"), ContentSource.Health, "Minuto de Saúde", "https://example.test", SynchronizationStage.Catalog);

        Assert.DoesNotContain("Bearer-secret", diagnostic.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=abc", diagnostic.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SynchronizationStage.Discovery, "descoberta")]
    [InlineData(SynchronizationStage.Download, "download")]
    [InlineData(SynchronizationStage.Validation, "validação")]
    [InlineData(SynchronizationStage.Extraction, "extração")]
    [InlineData(SynchronizationStage.Storage, "gravação")]
    [InlineData(SynchronizationStage.Catalog, "catálogo")]
    public void GetStageLabel_ShouldDescribeEverySynchronizationStage(SynchronizationStage stage, string expected)
    {
        Assert.Contains(expected, SynchronizationFailureClassifier.GetStageLabel(stage), StringComparison.OrdinalIgnoreCase);
    }
}
