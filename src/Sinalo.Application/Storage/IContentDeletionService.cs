namespace Sinalo.Application.Storage;

public interface IContentDeletionService
{
    Task DeleteAsync(string contentItemId, CancellationToken cancellationToken = default);
}
