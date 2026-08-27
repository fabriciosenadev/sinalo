namespace Sinalo.Application.Monitors;

public sealed record OutputProfile(
    string MonitorKey,
    string DisplayName,
    int ScreenNumber,
    int BoundsX,
    int BoundsY,
    int BoundsWidth,
    int BoundsHeight,
    bool IsPrimary);
