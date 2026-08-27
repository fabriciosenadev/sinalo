using Sinalo.Application.Monitors;

namespace Sinalo.Application.Presentation;

public sealed record PresentationScene(
    string Title,
    string MainText,
    string SecondaryText = "",
    bool ShowsTimerDetails = false,
    double ProgressValue = 0,
    double ProgressMaximum = 1,
    IReadOnlyList<string>? DetailItems = null);
public sealed record PresentationOutputResult(bool Succeeded, string Message);

public interface IPresentationOutputService
{
    bool IsOpen { get; }
    Task<PresentationOutputResult> ShowAsync(PresentationScene scene, OutputProfile requestedOutput, CancellationToken cancellationToken = default);
    Task UpdateAsync(PresentationScene scene, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}
