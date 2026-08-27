using Sinalo.Application.Playback;

namespace Sinalo.Application.Monitors;

public static class OutputSelectionResolver
{
    public static OutputProfile? Resolve(PlaybackConfiguration configuration, IReadOnlyList<OutputProfile> outputs)
    {
        if (outputs.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(configuration.FullscreenMonitorKey))
            return outputs.FirstOrDefault(output => string.Equals(output.MonitorKey, configuration.FullscreenMonitorKey, StringComparison.OrdinalIgnoreCase));

        if (configuration.FullscreenScreenNumber is int screenNumber)
        {
            var byScreenNumber = outputs.FirstOrDefault(output => output.ScreenNumber == screenNumber);
            if (byScreenNumber is not null) return byScreenNumber;
        }

        return outputs.FirstOrDefault(output => output.IsPrimary) ?? outputs[0];
    }
}
