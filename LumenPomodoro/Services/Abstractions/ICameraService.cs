using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface ICameraService : IDisposable
{
    bool IsRunning { get; }
    void Initialize(int cameraIndex, Action<string> statusCallback, Action<string> errorCallback, Action? onPresenceLost = null, Action? onPresenceRegained = null);
    Task StartCameraAsync();
    Task StartCameraForDurationAsync(int seconds);
    Task StopCameraAsync();
    Task<List<string>> GetAvailableCamerasAsync();
    Task<int> GetCameraCountAsync();
    void ClearCache();
}
