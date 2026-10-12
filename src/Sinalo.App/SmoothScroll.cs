using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sinalo.App;

/// <summary>Short wheel interpolation; native thumb, keyboard and virtualization are retained.</summary>
public static class SmoothScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(SmoothScroll));
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ScrollViewer viewer) return;
        if (args.NewValue is true)
        {
            var state = new State(viewer);
            viewer.SetValue(StateProperty, state);
            state.Attach();
        }
        else if (viewer.GetValue(StateProperty) is State state)
        {
            state.Detach();
            viewer.ClearValue(StateProperty);
        }
    }

    private sealed class State(ScrollViewer viewer)
    {
        private readonly WheelScrollMotion _motion = new();
        private readonly Stopwatch _clock = new();
        private bool _rendering;
        public void Attach()
        {
            viewer.PreviewMouseWheel += Wheel;
            viewer.PreviewMouseDown += Interrupt;
            viewer.PreviewKeyDown += Interrupt;
            viewer.Unloaded += Unloaded;
        }
        public void Detach()
        {
            Stop();
            viewer.PreviewMouseWheel -= Wheel;
            viewer.PreviewMouseDown -= Interrupt;
            viewer.PreviewKeyDown -= Interrupt;
            viewer.Unloaded -= Unloaded;
        }
        private void Unloaded(object sender, RoutedEventArgs args) => Stop();
        private void Interrupt(object sender, RoutedEventArgs args) => Stop();
        private void Wheel(object sender, MouseWheelEventArgs args)
        {
            // Let the innermost scrollable region consume the wheel. At its edge,
            // an enclosing region can scroll instead, without moving both regions.
            if (args.Handled || Keyboard.Modifiers != ModifierKeys.None) return;
            for (var node = args.OriginalSource as DependencyObject; node is not null && node != viewer;
                 node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            {
                if (node is System.Windows.Controls.ComboBox) return;
                if (node is ScrollViewer nested && CanMove(nested, args.Delta)) return;
            }
            if (!CanMove(viewer, args.Delta) || SystemParameters.WheelScrollLines == 0) return;
            var distance = SystemParameters.WheelScrollLines < 0
                ? viewer.ViewportHeight : Math.Min(viewer.ViewportHeight, SystemParameters.WheelScrollLines * 16d);
            var delta = -args.Delta / 120d * distance;
            _motion.Add(viewer.VerticalOffset, delta, viewer.ScrollableHeight, _clock.Elapsed.TotalMilliseconds);
            args.Handled = true;
            if (!SystemParameters.ClientAreaAnimation)
            {
                viewer.ScrollToVerticalOffset(_motion.Target);
                Stop();
                return;
            }
            if (!_rendering)
            {
                _rendering = true;
                CompositionTarget.Rendering += Render;
            }
            if (!_clock.IsRunning) _clock.Start();
        }
        private static bool CanMove(ScrollViewer scroll, int delta) =>
            scroll.ScrollableHeight > 0 && (delta < 0 ? scroll.VerticalOffset < scroll.ScrollableHeight - .1 : scroll.VerticalOffset > .1);
        private void Render(object? sender, EventArgs args)
        {
            viewer.ScrollToVerticalOffset(Math.Min(viewer.ScrollableHeight, _motion.Advance(_clock.Elapsed.TotalMilliseconds)));
            if (!_motion.IsActive || !viewer.IsVisible) Stop();
        }
        private void Stop()
        {
            if (_rendering) CompositionTarget.Rendering -= Render;
            _rendering = false;
            _motion.Cancel();
            _clock.Reset();
        }
    }
}

internal sealed class WheelScrollMotion
{
    internal const double DurationMilliseconds = 110;
    private double _from, _started;
    public double Target { get; private set; }
    public bool IsActive { get; private set; }
    public void Add(double offset, double delta, double limit, double now)
    {
        // Reversal discards the old destination so the UI follows the hand immediately.
        var continuing = IsActive && Math.Sign(delta) == Math.Sign(Target - offset);
        Target = Math.Clamp((continuing ? Target : offset) + delta, 0, Math.Max(0, limit));
        _from = offset;
        _started = now;
        IsActive = Math.Abs(Target - offset) > .01;
    }
    public double Advance(double now)
    {
        var progress = Math.Clamp((now - _started) / DurationMilliseconds, 0, 1);
        if (progress >= 1) IsActive = false;
        return _from + (Target - _from) * (1 - (1 - progress) * (1 - progress));
    }
    public void Cancel() => IsActive = false;
}
