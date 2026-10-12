using Sinalo.App;

namespace Sinalo.Tests.Unit;

public sealed class WheelScrollMotionTests
{
    [Fact]
    public void Wheel_RespondsOnFirstFrameAndFinishesWithoutOvershoot()
    {
        var motion = new WheelScrollMotion();
        motion.Add(0, 48, 1000, 0);
        Assert.True(motion.IsActive);
        Assert.InRange(motion.Advance(16), 10, 20);
        Assert.InRange(motion.Advance(55), 35, 37);
        Assert.Equal(48, motion.Advance(110));
        Assert.False(motion.IsActive);
        Assert.Equal(48, motion.Advance(500));
    }

    [Fact]
    public void RepeatedWheel_AccumulatesAndReversalRespondsImmediately()
    {
        var motion = new WheelScrollMotion();
        motion.Add(0, 48, 500, 0);
        var current = motion.Advance(16);
        motion.Add(current, 48, 500, 16);
        Assert.Equal(96, motion.Target);
        current = motion.Advance(32);
        motion.Add(current, -48, 500, 32);
        Assert.True(motion.Advance(48) < current);
        Assert.Equal(0, motion.Target);
        Assert.Equal(0, motion.Advance(142));
    }

    [Fact]
    public void LimitsCancellationAndNewGesture_AreBounded()
    {
        var motion = new WheelScrollMotion();
        motion.Add(90, 200, 100, 0);
        Assert.Equal(100, motion.Advance(110));
        motion.Add(0, -10, 100, 120);
        Assert.False(motion.IsActive);
        motion.Add(0, 48, 100, 130);
        motion.Cancel();
        motion.Add(10, 20, 100, 150);
        Assert.Equal(30, motion.Advance(260));
        motion.Add(0, 20, -10, 300);
        Assert.False(motion.IsActive);
    }
}
