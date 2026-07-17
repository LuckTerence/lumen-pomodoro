using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests;

/// <summary>
/// TimerEngine 是纯状态机（不依赖 DispatcherTimer），此处用虚拟时钟驱动，
/// 覆盖倒计时递减、完成、暂停/恢复、唤醒补偿等核心行为。
/// </summary>
public class TimerEngineTests
{
    private static readonly DateTime T0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StartFocus_SetsState()
    {
        var engine = new TimerEngine();
        engine.StartFocus(25, T0);
        Assert.Equal(TimerMode.Focus, engine.CurrentMode);
        Assert.True(engine.IsRunning);
        Assert.False(engine.IsPaused);
        Assert.Equal(25 * 60, engine.RemainingSeconds);
        Assert.Equal(25 * 60, engine.TotalSeconds);
    }

    [Fact]
    public void Advance_DecreasesOneSecondPerTick()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        var r1 = engine.Advance(T0.AddSeconds(1));
        Assert.True(r1.ShouldTick);
        Assert.False(r1.ShouldComplete);
        Assert.Equal(59, r1.RemainingSeconds);

        var r2 = engine.Advance(T0.AddSeconds(2));
        Assert.Equal(58, r2.RemainingSeconds);
    }

    [Fact]
    public void Advance_BeforeNextTickDoesNotFire()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        var r = engine.Advance(T0.AddMilliseconds(500));
        Assert.False(r.ShouldTick);
        Assert.False(r.ShouldComplete);
        Assert.Equal(60, r.RemainingSeconds);
    }

    [Fact]
    public void Advance_CompletesWhenTimeRunsOut()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        // 模拟真实 DispatcherTimer 每 ~1 秒推进一次（补偿上限会限制单次大跳跃）
        TimerAdvanceResult last = TimerAdvanceResult.NoTick(60, 60, TimerMode.Focus);
        for (int s = 1; s <= 60; s++)
        {
            last = engine.Advance(T0.AddSeconds(s));
        }
        Assert.True(last.ShouldComplete);
        Assert.Equal(TimerMode.Focus, last.CompletedMode);
        Assert.False(last.ShouldTick);
        Assert.Equal(0, engine.RemainingSeconds);
        Assert.False(engine.IsRunning);
        Assert.Equal(TimerMode.Idle, engine.CurrentMode);
    }

    [Fact]
    public void Pause_StopsCounting()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        Assert.True(engine.Pause(T0.AddSeconds(1)));
        Assert.True(engine.IsPaused);
        // 暂停期间推进时间不应递减
        var r = engine.Advance(T0.AddSeconds(30));
        Assert.False(r.ShouldTick);
        Assert.Equal(60, engine.RemainingSeconds);
    }

    [Fact]
    public void Resume_ContinuesCounting()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        Assert.True(engine.Pause(T0.AddSeconds(1)));
        Assert.True(engine.Resume(T0.AddSeconds(5)));
        var r = engine.Advance(T0.AddSeconds(6));
        Assert.True(r.ShouldTick);
        Assert.Equal(59, r.RemainingSeconds);
    }

    [Fact]
    public void StartFocus_WhileRunning_IsNoOp()
    {
        var engine = new TimerEngine();
        engine.StartFocus(25, T0);
        engine.StartFocus(50, T0.AddSeconds(1));
        Assert.Equal(25 * 60, engine.RemainingSeconds);
    }

    [Fact]
    public void ApplyWakeCorrection_IgnoresSmallGap()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        engine.Advance(T0.AddSeconds(1)); // remaining 59
        var r = engine.ApplyWakeCorrection(T0.AddSeconds(2.5)); // 间隔 1.5s < 2s，忽略
        Assert.False(r.ShouldTick);
        Assert.Equal(59, engine.RemainingSeconds);
    }

    [Fact]
    public void ApplyWakeCorrection_DeductsLargeGap()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        engine.Advance(T0.AddSeconds(1)); // remaining 59
        var r = engine.ApplyWakeCorrection(T0.AddSeconds(11)); // 间隔 10s
        Assert.True(r.ShouldTick);
        Assert.False(r.ShouldComplete);
        Assert.Equal(49, engine.RemainingSeconds);
    }

    [Fact]
    public void ApplyWakeCorrection_IgnoresHugeGap()
    {
        var engine = new TimerEngine();
        engine.StartFocus(1, T0);
        engine.Advance(T0.AddSeconds(1)); // remaining 59
        var r = engine.ApplyWakeCorrection(T0.AddSeconds(1 + 90000)); // > 24h，忽略
        Assert.False(r.ShouldTick);
        Assert.Equal(59, engine.RemainingSeconds);
    }

    [Fact]
    public void Advance_CapsCompensation()
    {
        var engine = new TimerEngine();
        engine.StartFocus(25, T0);
        // 模拟一次巨大跳跃（超过补偿上限 10s），剩余应只扣 10
        var r = engine.Advance(T0.AddSeconds(100));
        Assert.True(r.ShouldTick);
        Assert.False(r.ShouldComplete);
        Assert.Equal(25 * 60 - 10, engine.RemainingSeconds);
    }
}
