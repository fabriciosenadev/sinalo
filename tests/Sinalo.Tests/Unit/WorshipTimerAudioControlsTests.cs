using Sinalo.App;
using Sinalo.App.ViewModels;
using Sinalo.Application.WorshipTimer;

namespace Sinalo.Tests.Unit;

public sealed class WorshipTimerAudioControlsTests
{
    [Fact]
    public void StopTimer_ShouldAlsoStopCurrentAudio()
    {
        var player = new FakeAudioPlayer();
        var viewModel = new WorshipTimerViewModel(new WorshipTimerSession(), player);

        viewModel.StartOrStop();
        Assert.False(viewModel.IsConfigurationEditable);
        viewModel.PlaySelectedAudio();
        viewModel.StartOrStop();

        Assert.Equal(WorshipTimerAudioPlaybackState.Stopped, player.Snapshot.State);
        Assert.True(viewModel.IsConfigurationEditable);
    }

    [Fact]
    public void AutomaticCue_ShouldReplaceManualPlayback()
    {
        var player = new FakeAudioPlayer();
        var viewModel = new WorshipTimerViewModel(new WorshipTimerSession(), player);

        viewModel.PlaySelectedAudio();
        viewModel.PlayAutomaticCue(WorshipTimerAudioCue.OneMinute);

        Assert.Equal(WorshipTimerAudioCue.OneMinute, player.Snapshot.Cue);
        Assert.Equal(WorshipTimerAudioPlaybackOrigin.Automatic, player.Snapshot.Origin);
    }

    private sealed class FakeAudioPlayer : IWorshipTimerAudioPlayer
    {
        public event EventHandler<WorshipTimerAudioSnapshot>? StateChanged;
        public WorshipTimerAudioSnapshot Snapshot { get; private set; } = WorshipTimerAudioSnapshot.Initial;

        public void Play(WorshipTimerAudioCue cue, WorshipTimerAudioPlaybackOrigin origin) => Set(new(cue, WorshipTimerAudioPlaybackState.Playing, origin, TimeSpan.Zero, TimeSpan.FromSeconds(10), Snapshot.Volume));
        public void Pause() => Set(Snapshot with { State = WorshipTimerAudioPlaybackState.Paused });
        public void Resume() => Set(Snapshot with { State = WorshipTimerAudioPlaybackState.Playing });
        public void Stop() => Set(new(null, WorshipTimerAudioPlaybackState.Stopped, null, TimeSpan.Zero, TimeSpan.Zero, Snapshot.Volume));
        public void Seek(TimeSpan position) => Set(Snapshot with { Position = position });
        public void SetVolume(double volume) => Set(Snapshot with { Volume = volume });
        public void Refresh() { }

        private void Set(WorshipTimerAudioSnapshot snapshot)
        {
            Snapshot = snapshot;
            StateChanged?.Invoke(this, snapshot);
        }
    }
}
