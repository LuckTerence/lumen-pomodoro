using LumenPomodoro.Models;

namespace LumenPomodoro.Tests.Models;

public class ModelTests
{
    [Fact]
    public void TaskItem_Model_ShouldHaveDefaultValues()
    {
        var task = new TaskItem();

        Assert.False(string.IsNullOrEmpty(task.Id));
        Assert.Equal(string.Empty, task.Name);
        Assert.Equal(string.Empty, task.Category);
        Assert.Equal("#3B82F6", task.Color);
    }

    [Fact]
    public void FocusSession_Model_ShouldHaveDefaultValues()
    {
        var session = new FocusSession();

        Assert.False(string.IsNullOrEmpty(session.Id));
        Assert.Equal(25, session.FocusMinutes);
        Assert.False(session.Completed);
        Assert.Equal(0, session.QualityScore);
        Assert.Null(session.EndTime);
    }

    [Fact]
    public void Settings_Model_ShouldHaveDefaultValues()
    {
        var settings = new Settings();

        Assert.Equal(25, settings.WorkMinutes);
        Assert.Equal(5, settings.ShortBreakMinutes);
        Assert.Equal(15, settings.LongBreakMinutes);
        Assert.Equal(4, settings.LongBreakInterval);
        Assert.False(settings.CameraAlertEnabled);
        Assert.True(settings.FocusGuardEnabled);
        Assert.Equal(2, settings.FocusGuardDebounceHits);
        Assert.Equal(3, settings.FocusGuardMaxAlertsPerSession);
        Assert.True(settings.FocusGuardRespectDoNotDisturb);
        Assert.True(settings.ConfirmExitWhileFocusing);
        Assert.Equal(30, settings.SessionEndPreNotifySeconds);
        Assert.False(settings.FullscreenBreakEnabled);
        Assert.False(settings.StrictModeEnabled);
        Assert.True(settings.EffectiveCameraAlertCanManualClose);
        Assert.True(settings.EffectiveAllowEndBreakEarly);
        Assert.Equal(CameraAlertMode.UntilConfirm, settings.CameraAlertMode);
        Assert.Equal(180, settings.CameraFixedOnSeconds);
        Assert.True(settings.SoundEnabled);
        Assert.True(settings.PopupEnabled);
        Assert.True(settings.SystemNotificationEnabled);
        Assert.False(settings.TrayEnabled);
        Assert.False(settings.CloseToTray);
        Assert.False(settings.AutoStartEnabled);
        Assert.Equal("system", settings.Theme);
        Assert.True(settings.AnimationEnabled);

        settings.StrictModeEnabled = true;
        Assert.False(settings.EffectiveCameraAlertCanManualClose);
        Assert.False(settings.EffectiveAllowEndBreakEarly);
    }

    [Fact]
    public void Settings_ApplyStrictFocusPreset_EnablesCompanionOptions()
    {
        var settings = new Settings
        {
            StrictModeEnabled = false,
            FullscreenBreakEnabled = false,
            CameraAlertEnabled = false,
            CameraAlertCanManualClose = true,
            SessionEndPreNotifySeconds = 0
        };

        settings.ApplyStrictFocusPreset();

        Assert.True(settings.StrictModeEnabled);
        Assert.True(settings.FullscreenBreakEnabled);
        Assert.True(settings.CameraAlertEnabled);
        Assert.Equal(CameraAlertLevel.Severe, settings.CameraAlertLevel);
        Assert.False(settings.CameraAlertCanManualClose);
        Assert.True(settings.CameraFollowBreakEnabled);
        Assert.True(settings.ConfirmExitWhileFocusing);
        Assert.Equal(30, settings.SessionEndPreNotifySeconds);
        Assert.False(settings.EffectiveCameraAlertCanManualClose);
        Assert.True(settings.SoundEnabled);
        Assert.True(settings.PopupEnabled);
        Assert.True(settings.SystemNotificationEnabled);
    }

    [Fact]
    public void Settings_ApplyLightAndStandardPresets()
    {
        var settings = new Settings { CameraAlertEnabled = true, FocusGuardEnabled = true };
        settings.ApplyLightFocusPreset();
        Assert.False(settings.CameraAlertEnabled);
        Assert.False(settings.FocusGuardEnabled);
        Assert.False(settings.StrictModeEnabled);

        settings.ApplyStandardFocusPreset();
        Assert.True(settings.CameraAlertEnabled);
        Assert.True(settings.FocusGuardEnabled);
        Assert.Equal(CameraAlertLevel.Medium, settings.CameraAlertLevel);
        Assert.False(settings.StrictModeEnabled);
    }

    [Theory]
    [InlineData(false, false, null, true, "关闭")]
    [InlineData(true, true, "运行中", true, "亮着")]
    [InlineData(true, false, null, true, "待命")]
    public void CameraAlertStatusText_Describe_ContainsKeyword(
        bool enabled, bool active, string? raw, bool canClose, string keyword)
    {
        var text = CameraAlertStatusText.Describe(enabled, active, raw, canClose);
        Assert.Contains(keyword, text);
    }
}
