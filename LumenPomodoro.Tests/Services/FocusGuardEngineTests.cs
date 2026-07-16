using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests.Services;

public class FocusGuardEngineTests
{
    [Fact]
    public void Tick_SingleHit_DoesNotFire_WhenDebounceIsTwo()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(debounceHits: 2, maxAlertsPerSession: 3);

        var r = engine.Tick("bilibili");

        Assert.False(r.FireDistraction);
        Assert.False(engine.IsDistracted);
        Assert.Equal(1, engine.ConsecutiveHits);
        Assert.Equal(0, engine.AlertCount);
    }

    [Fact]
    public void Tick_TwoHits_FiresDistractionOnce()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(2, 3);

        Assert.False(engine.Tick("idle").FireDistraction);
        var second = engine.Tick("idle");

        Assert.True(second.FireDistraction);
        Assert.Equal("idle", second.Reason);
        Assert.True(engine.IsDistracted);
        Assert.Equal(1, engine.AlertCount);

        // 仍处于分心态时继续命中，不再重复告警
        var third = engine.Tick("idle");
        Assert.False(third.FireDistraction);
        Assert.Equal(1, engine.AlertCount);
    }

    [Fact]
    public void Tick_OscillatingHits_RespectsDebounce()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(2, 3);

        engine.Tick("x");           // hit 1
        engine.Tick(null);          // clear
        engine.Tick("x");           // hit 1 again
        var r = engine.Tick("x");   // hit 2 → fire

        Assert.True(r.FireDistraction);
    }

    [Fact]
    public void Tick_MaxAlerts_BlocksFurtherNotifications_KeepsDistractedState()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(debounceHits: 1, maxAlertsPerSession: 3);

        for (int i = 0; i < 3; i++)
        {
            var fire = engine.Tick($"r{i}");
            Assert.True(fire.FireDistraction, $"alert {i + 1} should fire");
            engine.Tick(null); // regain — does NOT reset alert count
        }

        Assert.Equal(3, engine.AlertCount);

        var blocked = engine.Tick("again");
        Assert.False(blocked.FireDistraction);
        Assert.True(engine.IsDistracted);
        Assert.Equal(3, engine.AlertCount);
    }

    [Fact]
    public void Tick_Regained_DoesNotResetAlertCount()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(1, 5);

        engine.Tick("a");
        Assert.Equal(1, engine.AlertCount);

        var regained = engine.Tick(null);
        Assert.True(regained.FireRegained);
        Assert.Equal(1, engine.AlertCount);
        Assert.False(engine.IsDistracted);
    }

    [Fact]
    public void ResetSession_ClearsAlerts()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(1, 3);
        engine.Tick("x");
        Assert.Equal(1, engine.AlertCount);

        engine.ResetSession();

        Assert.Equal(0, engine.AlertCount);
        Assert.False(engine.IsDistracted);
        Assert.Equal(0, engine.ConsecutiveHits);
    }

    [Fact]
    public void Configure_ClampsInvalidValues()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(debounceHits: 0, maxAlertsPerSession: 99);

        Assert.Equal(1, engine.DebounceHits);
        Assert.Equal(20, engine.MaxAlertsPerSession);
    }

    [Fact]
    public void Tick_SuppressNotification_DoesNotCountOrFire()
    {
        var engine = new FocusGuardEngine();
        engine.Configure(1, 3);

        var r = engine.Tick("dnd", suppressNotification: true);

        Assert.False(r.FireDistraction);
        Assert.True(engine.IsDistracted);
        Assert.Equal(0, engine.AlertCount);

        // 离开勿扰后再次进入分心才计数
        engine.Tick(null);
        var again = engine.Tick("after", suppressNotification: false);
        Assert.True(again.FireDistraction);
        Assert.Equal(1, engine.AlertCount);
    }
}

public class FocusGuardServiceTests
{
    private static Settings TestSettings(
        int debounce = 2,
        int maxAlerts = 3) => new()
    {
        FocusGuardEnabled = true,
        FocusGuardIdleSeconds = 180,
        FocusGuardPollSeconds = 60, // 慢轮询，测试只用 ProcessEvaluation
        FocusGuardDebounceHits = debounce,
        FocusGuardMaxAlertsPerSession = maxAlerts,
        FocusGuardBlocklist = new List<string> { "bilibili" }
    };

    [Fact]
    public void ProcessEvaluation_DebounceAndMaxAlerts_EndToEnd()
    {
        using var svc = new FocusGuardService();
        var distractions = new List<string>();
        var regained = 0;
        svc.DistractionDetected += r => distractions.Add(r);
        svc.FocusRegained += () => regained++;

        svc.Start(TestSettings(debounce: 2, maxAlerts: 2));

        svc.ProcessEvaluation("first");
        Assert.Empty(distractions);

        svc.ProcessEvaluation("first");
        Assert.Single(distractions);

        svc.ProcessEvaluation(null);
        Assert.Equal(1, regained);

        svc.ProcessEvaluation("second");
        svc.ProcessEvaluation("second");
        Assert.Equal(2, distractions.Count);

        svc.ProcessEvaluation(null);
        svc.ProcessEvaluation("third");
        svc.ProcessEvaluation("third");
        // 第 3 次进入分心不再通知
        Assert.Equal(2, distractions.Count);
        Assert.Equal(2, svc.AlertCount);
    }

    [Fact]
    public void Start_Disabled_DoesNothing()
    {
        using var svc = new FocusGuardService();
        svc.Start(new Settings { FocusGuardEnabled = false });
        Assert.False(svc.IsRunning);
    }

    [Fact]
    public void Start_WithoutReset_KeepsAlertCount()
    {
        using var svc = new FocusGuardService();
        var count = 0;
        svc.DistractionDetected += _ => count++;

        svc.Start(TestSettings(1, 3), resetSessionCounters: true);
        svc.ProcessEvaluation("a");
        Assert.Equal(1, count);
        Assert.Equal(1, svc.AlertCount);

        svc.Stop();
        svc.Start(TestSettings(1, 3), resetSessionCounters: false);
        Assert.Equal(1, svc.AlertCount);

        svc.ProcessEvaluation("b");
        Assert.Equal(2, count);
    }

    [Fact]
    public void ProcessEvaluation_WhenDoNotDisturb_SuppressesNotification()
    {
        using var svc = new FocusGuardService();
        var count = 0;
        svc.DistractionDetected += _ => count++;
        svc.DoNotDisturbOverride = () => true;

        var settings = TestSettings(1, 3);
        settings.FocusGuardRespectDoNotDisturb = true;
        svc.Start(settings);

        svc.ProcessEvaluation("x");
        Assert.Equal(0, count);
        Assert.Equal(0, svc.AlertCount);

        svc.DoNotDisturbOverride = () => false;
        svc.ProcessEvaluation(null);
        svc.ProcessEvaluation("y");
        Assert.Equal(1, count);
        Assert.Equal(1, svc.AlertCount);
    }
}
