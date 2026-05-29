using LumenPomodoro.Models;

namespace LumenPomodoro.Tests.Models;

public class ModelTests
{
    [Fact]
    public void FocusSession_Model_ShouldHaveCorrectProperties()
    {
        // Arrange
        var session = new FocusSession
        {
            Id = "test_1",
            TaskId = "task_1",
            TaskName = "Test",
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddMinutes(25),
            Completed = true,
            FocusMinutes = 25
        };

        // Assert
        Assert.Equal("test_1", session.Id);
        Assert.Equal("task_1", session.TaskId);
        Assert.Equal("Test", session.TaskName);
        Assert.True(session.Completed);
        Assert.Equal(25, session.FocusMinutes);
        Assert.NotNull(session.EndTime);
    }

    [Fact]
    public void TaskItem_Model_ShouldHaveCorrectProperties()
    {
        // Arrange
        var task = new TaskItem
        {
            Id = "task_1",
            Name = "Test Task",
            Category = "Math",
            Color = "#FF0000",
            CreatedAt = DateTime.Now
        };

        // Assert
        Assert.Equal("task_1", task.Id);
        Assert.Equal("Test Task", task.Name);
        Assert.Equal("Math", task.Category);
        Assert.Equal("#FF0000", task.Color);
        Assert.True(task.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public void Settings_Model_ShouldHaveDefaultValues()
    {
        // Arrange
        var settings = new Settings();

        // Assert
        Assert.Equal(25, settings.WorkMinutes);
        Assert.Equal(5, settings.ShortBreakMinutes);
        Assert.Equal(15, settings.LongBreakMinutes);
        Assert.Equal(4, settings.LongBreakInterval);
        Assert.False(settings.CameraAlertEnabled);
        Assert.True(settings.FocusGuardEnabled);
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
    }
}
