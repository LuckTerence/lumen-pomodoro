using System.Windows;
using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface ITrayService : IDisposable
{
    void AttachToWindow(Window window);
    void UpdateMenuState();
    void ShowNotification(string title, string message);
}
