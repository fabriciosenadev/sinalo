namespace Sinalo.Application.Storage;

public interface ISinaloPathService
{
    SinaloPaths GetPaths();

    void EnsureFolders();
}
