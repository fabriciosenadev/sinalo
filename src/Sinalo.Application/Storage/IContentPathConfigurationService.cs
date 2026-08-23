namespace Sinalo.Application.Storage;

public interface IContentPathConfigurationService
{
    string GetContentPath();

    void SaveContentPath(string contentPath);
}
