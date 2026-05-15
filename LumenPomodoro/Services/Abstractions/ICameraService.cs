namespace LumenPomodoro.Services.Abstractions;

public interface ICameraService : IDisposable
{
    Task InitializeAsync(int cameraIndex, Action<string>? onStatusChanged = null, Action<bool>? onStateChanged = null, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task StartCameraForDurationAsync(int seconds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CameraDevice>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default);
    Task<int> GetCameraCountAsync(CancellationToken cancellationToken = default);
    bool IsActive { get; }
    bool IsRunning { get; }
    string Status { get; }
}

public record CameraDevice(string Id, string Name);
