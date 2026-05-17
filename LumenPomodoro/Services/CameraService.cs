using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public class CameraService : ICameraService
{
    private volatile bool _isRunning = false;
    private volatile bool _isInitializing = false;
    private Task? _cameraTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private int _cameraIndex = 0;
    private Action<string>? _statusCallback;
    private Action<string>? _errorCallback;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private readonly object _lockObject = new object();
    private DateTime? _startTime;
    private const int MaxRunMinutes = 30;
    private List<string>? _cachedCameraNames;
    private int _cachedCameraCount = -1;

    private static List<DeviceInformation>? _cachedDevices;

    private readonly PresenceDetector _presenceDetector = new();
    private Action? _presenceLostCallback;
    private DateTime _lastProcessedTime = DateTime.MinValue;
    private const int MinProcessIntervalMs = 1000;
    private bool _wasPresent = true;

    public bool IsRunning => _isRunning;

    public void Initialize(int cameraIndex, Action<string> statusCallback, Action<string> errorCallback, Action? onPresenceLost = null)
    {
        _cameraIndex = cameraIndex;
        _statusCallback = statusCallback;
        _errorCallback = errorCallback;
        _presenceLostCallback = onPresenceLost;
        _presenceDetector.Reset();
        _wasPresent = true;
        Log.Debug("CameraService 初始化，摄像头索引: {Index}", cameraIndex);
    }

    public async Task StartCameraAsync()
    {
        lock (_lockObject)
        {
            if (_isRunning || _isInitializing) return;
            _isInitializing = true;
        }

        _startTime = DateTime.Now;

        var newCts = new CancellationTokenSource();
        var oldCts = _cancellationTokenSource;
        _cancellationTokenSource = newCts;
        if (oldCts != null)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        try
        {
            _statusCallback?.Invoke("正在初始化摄像头...");
            Log.Information("正在启动摄像头...");

            await InitializeCameraDeviceAsync(_cancellationTokenSource.Token);

            lock (_lockObject)
            {
                _isRunning = true;
            }

            _statusCallback?.Invoke("摄像头提醒中：当前摄像头被用于点亮指示灯，不会保存或上传画面。");
            Log.Information("摄像头已启动");

            _cameraTask = KeepCameraActiveAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            lock (_lockObject)
            {
                _isRunning = false;
            }

            string errorMsg = ex switch
            {
                UnauthorizedAccessException => "摄像头权限被拒绝，请前往 Windows 隐私设置开启摄像头权限",
                _ when ex.Message.Contains("0x80070005") || ex.Message.Contains("E_ACCESSDENIED") => "摄像头权限被拒绝，请前往 Windows 隐私设置开启摄像头权限",
                _ => $"摄像头打开失败: {ex.Message}"
            };
            Log.Error(ex, "摄像头启动失败");
            _errorCallback?.Invoke(errorMsg);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task InitializeCameraDeviceAsync(CancellationToken token)
    {
        var devices = await EnumerateCamerasAsync();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("未检测到可用的摄像头设备");
        }

        var deviceIndex = Math.Min(_cameraIndex, devices.Count - 1);
        var device = devices[deviceIndex];

        Log.Debug("初始化摄像头: {Name} (Id={Id})", device.Name, device.Id);

        var sourceGroups = await MediaFrameSourceGroup.FindAllAsync();
        var sourceGroup = sourceGroups.FirstOrDefault(group =>
            group.SourceInfos.Any(source => IsVideoSourceForDevice(source, device.Id)));

        if (sourceGroup == null)
        {
            throw new InvalidOperationException($"摄像头 {device.Name} 没有可读取的视频源");
        }

        var mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            SourceGroup = sourceGroup,
            StreamingCaptureMode = StreamingCaptureMode.Video,
            SharingMode = MediaCaptureSharingMode.ExclusiveControl,
            MemoryPreference = MediaCaptureMemoryPreference.Auto
        };

        await mediaCapture.InitializeAsync(settings);

        var frameSource = mediaCapture.FrameSources.Values.FirstOrDefault(source =>
            source.Info.MediaStreamType == MediaStreamType.VideoPreview ||
            source.Info.MediaStreamType == MediaStreamType.VideoRecord);

        if (frameSource == null)
        {
            mediaCapture.Dispose();
            throw new InvalidOperationException($"摄像头 {device.Name} 初始化成功，但没有可用的视频帧源");
        }

        await TrySetLowCostFormatAsync(frameSource);

        var frameReader = await mediaCapture.CreateFrameReaderAsync(frameSource);
        frameReader.FrameArrived += OnFrameArrived;

        var startStatus = await frameReader.StartAsync();
        if (startStatus != MediaFrameReaderStartStatus.Success)
        {
            frameReader.FrameArrived -= OnFrameArrived;
            frameReader.Dispose();
            mediaCapture.Dispose();
            throw new InvalidOperationException($"摄像头视频流启动失败: {startStatus}");
        }

        lock (_lockObject)
        {
            _mediaCapture = mediaCapture;
            _frameReader = frameReader;
        }

        Log.Debug("MediaCapture 视频流启动成功");
    }

    private static async Task TrySetLowCostFormatAsync(MediaFrameSource frameSource)
    {
        var format = frameSource.SupportedFormats
            .Where(item => item.VideoFormat != null)
            .OrderBy(GetFrameRate)
            .ThenBy(item => (long)item.VideoFormat.Width * item.VideoFormat.Height)
            .FirstOrDefault();

        if (format == null) return;

        try
        {
            await frameSource.SetFormatAsync(format);
            Log.Debug("使用低成本摄像头格式: {Width}x{Height}, {Fps:F1}fps",
                format.VideoFormat.Width, format.VideoFormat.Height, GetFrameRate(format));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "设置低成本摄像头格式失败");
        }
    }

    private static double GetFrameRate(MediaFrameFormat format)
    {
        var frameRate = format.FrameRate;
        if (frameRate == null || frameRate.Denominator == 0) return double.MaxValue;
        return (double)frameRate.Numerator / frameRate.Denominator;
    }

    private static bool IsVideoSourceForDevice(MediaFrameSourceInfo source, string deviceId)
    {
        return (source.MediaStreamType == MediaStreamType.VideoPreview ||
                source.MediaStreamType == MediaStreamType.VideoRecord) &&
               string.Equals(source.DeviceInformation?.Id, deviceId, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastProcessedTime).TotalMilliseconds < MinProcessIntervalMs) return;
        _lastProcessedTime = now;

        using var frame = sender.TryAcquireLatestFrame();
        if (frame?.VideoMediaFrame?.SoftwareBitmap == null) return;

        try
        {
            var bitmap = frame.VideoMediaFrame.SoftwareBitmap;
            if (bitmap.BitmapPixelFormat != Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8)
                return;

            var buffer = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyToBuffer(buffer.AsBuffer());

            bool present = _presenceDetector.ProcessFrame(buffer, bitmap.PixelWidth, bitmap.PixelHeight, bitmap.PixelWidth * 4);

            // 只在"从有人变为无人"的瞬间触发回调，避免重复通知
            if (!present && _wasPresent)
            {
                _presenceLostCallback?.Invoke();
            }
            _wasPresent = present;
        }
        catch
        {
            // 帧处理失败不影响主流程
        }
    }

    public async Task StartCameraForDurationAsync(int seconds)
    {
        await StartCameraAsync();
        if (!_isRunning) return;

        try
        {
            await Task.Delay(seconds * 1000, _cancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        await StopCameraAsync();
    }

    public async Task StopCameraAsync()
    {
        if (!_isRunning) return;

        _cancellationTokenSource?.Cancel();

        if (_cameraTask != null)
        {
            try
            {
                await _cameraTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "停止摄像头任务异常");
            }
        }

        StopCameraDevice();

        lock (_lockObject)
        {
            _isRunning = false;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _statusCallback?.Invoke("摄像头已关闭");
        Log.Information("摄像头已关闭");
    }

    private void StopCameraDevice()
    {
        lock (_lockObject)
        {
            if (_frameReader != null)
            {
                try
                {
                    _frameReader.FrameArrived -= OnFrameArrived;
                    // 异步停止，避免 .Wait() 阻塞 UI 线程
                    var stopTask = _frameReader.StopAsync().AsTask();
                    if (stopTask.Wait(3000))
                    {
                        _frameReader.Dispose();
                    }
                    else
                    {
                        Log.Warning("FrameReader.StopAsync 超时，强制 Dispose");
                        try { _frameReader.Dispose(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "停止摄像头读取器异常");
                    try { _frameReader.Dispose(); } catch { }
                }
                finally
                {
                    _frameReader = null;
                }
            }

            if (_mediaCapture != null)
            {
                try
                {
                    _mediaCapture.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "停止摄像头设备异常");
                }
                finally
                {
                    _mediaCapture = null;
                }
            }

            _presenceDetector.Reset();
            _wasPresent = true;
        }
    }

    private async Task KeepCameraActiveAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            lock (_lockObject)
            {
                if (_startTime.HasValue && (DateTime.Now - _startTime.Value).TotalMinutes >= MaxRunMinutes)
                {
                    if (_frameReader != null)
                    {
                        try
                        {
                            _frameReader.FrameArrived -= OnFrameArrived;
                            var stopTask = _frameReader.StopAsync().AsTask();
                            if (!stopTask.Wait(3000))
                                Log.Warning("KeepCameraActive: FrameReader.StopAsync 超时");
                            _frameReader.Dispose();
                        }
                        catch { }
                        _frameReader = null;
                    }
                    if (_mediaCapture != null)
                    {
                        try { _mediaCapture.Dispose(); } catch { }
                        _mediaCapture = null;
                    }
                    _isRunning = false;
                    var msg = $"摄像头已运行超过 {MaxRunMinutes} 分钟，自动保护释放";
                    Log.Warning(msg);
                    _errorCallback?.Invoke(msg);
                    return;
                }
            }
        }
    }

    public async Task<List<string>> GetAvailableCamerasAsync()
    {
        if (_cachedCameraNames != null) return _cachedCameraNames;

        try
        {
            var devices = await EnumerateCamerasAsync();
            _cachedCameraNames = devices.Select(d => d.Name).ToList();
            _cachedCameraCount = _cachedCameraNames.Count;

            if (_cachedCameraNames.Count == 0)
            {
                _cachedCameraNames = new List<string> { "默认摄像头" };
                _cachedCameraCount = 1;
            }

            return _cachedCameraNames;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取摄像头列表失败");
            _cachedCameraNames = new List<string> { "默认摄像头" };
            _cachedCameraCount = 1;
            return _cachedCameraNames;
        }
    }

    public async Task<int> GetCameraCountAsync()
    {
        if (_cachedCameraCount >= 0) return _cachedCameraCount;

        try
        {
            _cachedCameraCount = (await EnumerateCamerasAsync()).Count;
            if (_cachedCameraCount == 0) _cachedCameraCount = 1;
            return _cachedCameraCount;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取摄像头数量失败");
            _cachedCameraCount = 1;
            return _cachedCameraCount;
        }
    }

    public void ClearCache()
    {
        _cachedCameraNames = null;
        _cachedCameraCount = -1;
        _cachedDevices = null;
    }

    private static async Task<List<DeviceInformation>> EnumerateCamerasAsync()
    {
        if (_cachedDevices != null) return _cachedDevices;

        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        _cachedDevices = devices.ToList();
        return _cachedDevices;
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            try
            {
                StopCameraAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispose 停止摄像头异常");
            }
        }
    }
}
