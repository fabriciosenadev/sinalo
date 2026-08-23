namespace Sinalo.Application.Storage;

public interface IContentPathMigrationService
{
    Task MoveAsync(string newContentPath, CancellationToken cancellationToken = default);
}
