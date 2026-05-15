using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface ITrayService : IDisposable
{
    void Initialize(object window, object viewModel);
    void UpdateMenuState(TimerMode currentMode, string remainingTime);
    void ShowNotification(string title, string message);
    bool IsVisible { get; set; }
}
