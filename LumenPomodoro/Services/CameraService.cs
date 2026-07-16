using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LumenPomodoro.Interop;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public sealed class CameraService : ICameraService
{
    private volatile bool _isRunning;
    private volatile bool _isInitializing;
    private volatile CancellationTokenSource? _cancellationTokenSource;
    private Task? _cameraTask;
    private int _cameraIndex;
    private Action<string>? _statusCallback;
    private Action<string>? _errorCallback;
    private readonly object _lock = new();

    private bool _mfStarted;
    private IntPtr _mediaSource;
    private IntPtr _sourceReader;
    private DateTime _startTime;

    private volatile List<string>? _cachedCameraNames;
    private int _cachedCameraCount = -1;

    private const int MaxRunMinutes = 30;
    private static readonly TimeSpan CameraStopTimeout = TimeSpan.FromSeconds(2);
    private const int CameraPollIntervalMs = 100;

    public bool IsRunning => _isRunning;

    public void Initialize(int cameraIndex, Action<string> statusCallback,
        Action<string> errorCallback, Action? onPresenceLost = null,
        Action? onPresenceRegained = null)
    {
        _cameraIndex = cameraIndex;
        _statusCallback = statusCallback;
        _errorCallback = errorCallback;
    }

    public Task StartCameraAsync()
    {
        lock (_lock)
        {
            if (_isRunning || _isInitializing) return Task.CompletedTask;
            _isInitializing = true;
        }

        _startTime = DateTime.Now;
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        try
        {
            var cb = _statusCallback; cb?.Invoke("正在初始化摄像头...");
            EnsureMfStarted();
            OpenSelectedCamera();

            lock (_lock) { _isRunning = true; }
            cb = _statusCallback; cb?.Invoke("摄像头提醒中：当前摄像头被用于点亮指示灯，不会保存或上传画面。");
            _cameraTask = Task.Run(() => KeepCameraActiveAsync(newCts.Token), newCts.Token);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "摄像头启动失败");
            newCts.Cancel();
            newCts.Dispose();
            Interlocked.CompareExchange(ref _cancellationTokenSource, null, newCts);
            StopDevice();
            lock (_lock) { _isRunning = false; }

            var msg = ex switch
            {
                UnauthorizedAccessException => "摄像头权限被拒绝，请前往 Windows 隐私设置开启摄像头权限",
                _ when ex.Message.Contains("0x80070005") || ex.Message.Contains("E_ACCESSDENIED") =>
                    "摄像头权限被拒绝，请前往 Windows 隐私设置开启摄像头权限",
                _ => $"摄像头打开失败: {ex.Message}"
            };
            var ecb = _errorCallback; ecb?.Invoke(msg);
        }
        finally
        {
            lock (_lock) { _isInitializing = false; }
        }

        return Task.CompletedTask;
    }

    public async Task StartCameraForDurationAsync(int seconds)
    {
        await StartCameraAsync();
        if (!_isRunning) return;

        try
        {
            await Task.Delay(seconds * 1000, _cancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        await StopCameraAsync();
    }

    public async Task StopCameraAsync()
    {
        if (!_isRunning && _sourceReader == IntPtr.Zero && _mediaSource == IntPtr.Zero) return;

        // 捕获本地 CTS 引用，防止并发 StartCameraAsync 覆盖 _cancellationTokenSource
        var cts = _cancellationTokenSource;
        cts?.Cancel();

        var cameraTask = Volatile.Read(ref _cameraTask);
        if (cameraTask != null)
        {
            try
            {
                await cameraTask.WaitAsync(CameraStopTimeout);
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException)
            {
                Log.Warning("停止摄像头读取循环超时，继续释放摄像头资源");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "停止摄像头任务异常");
            }
        }

        StopDevice();
        lock (_lock) { _isRunning = false; }
        _cameraTask = null;
        cts?.Dispose();
        if (ReferenceEquals(cts, _cancellationTokenSource))
            _cancellationTokenSource = null;
        var cb = _statusCallback; cb?.Invoke("摄像头已关闭");
    }

    public Task<List<string>> GetAvailableCamerasAsync()
    {
        var names = _cachedCameraNames;
        if (names != null) return Task.FromResult(names);

        try
        {
            names = EnumerateDeviceNames();
            _cachedCameraNames = names.Count > 0 ? names : new List<string> { "默认摄像头" };
            _cachedCameraCount = names.Count;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取摄像头列表失败");
            _cachedCameraNames = new List<string> { "默认摄像头" };
            _cachedCameraCount = 1;
        }

        return Task.FromResult(_cachedCameraNames);
    }

    public Task<int> GetCameraCountAsync()
    {
        if (_cachedCameraCount >= 0) return Task.FromResult(_cachedCameraCount);

        try { _cachedCameraCount = Math.Max(1, EnumerateDeviceNames().Count); }
        catch (Exception ex) { Log.Debug(ex, "摄像头枚举失败，使用默认计数 1"); _cachedCameraCount = 1; }

        return Task.FromResult(_cachedCameraCount);
    }

    public void ClearCache()
    {
        _cachedCameraNames = null;
        _cachedCameraCount = -1;
    }

    public void Dispose()
    {
        if (_isRunning)
        {
            try
            {
                var task = StopCameraAsync();
                if (!task.Wait(5000)) Log.Warning("Dispose 停止摄像头超时");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispose 停止摄像头异常");
            }
        }

        ShutdownMf();
    }

    private void OpenSelectedCamera()
    {
        var (activateArray, count) = EnumerateActivates();
        Log.Information("Camera: found {Count} device(s)", count);
        if (activateArray == IntPtr.Zero || count <= 0)
            throw new InvalidOperationException("未检测到可用的摄像头设备");

        try
        {
            var index = Math.Clamp(_cameraIndex, 0, count - 1);
            var activate = Marshal.ReadIntPtr(activateArray, index * IntPtr.Size);
            var name = ReadActivateString(activate, MfConst.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME)
                ?? $"摄像头 {index}";

            var hr = ActivateDevice(activate, MfConst.IID_IMFMediaSource, out var mediaSource);
            Log.Information("Camera: ActivateObject {Name} = 0x{Code:X8}", name, hr);
            if (hr < 0 || mediaSource == IntPtr.Zero)
                throw new InvalidOperationException($"无法打开 {name} (0x{hr:X8})");

            hr = MfNative.MFCreateSourceReaderFromMediaSource(mediaSource, IntPtr.Zero, out var reader);
            Log.Information("Camera: CreateSourceReader = 0x{Code:X8}", hr);
            if (hr < 0 || reader == IntPtr.Zero)
            {
                ComVtbl.Release(mediaSource);
                throw new InvalidOperationException($"创建视频读取器失败 (0x{hr:X8})");
            }

            lock (_lock)
            {
                _mediaSource = mediaSource;
                _sourceReader = reader;
            }
        }
        finally
        {
            FreeActivateArray(activateArray, count);
        }
    }

    private async Task KeepCameraActiveAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if ((DateTime.Now - _startTime).TotalMinutes >= MaxRunMinutes)
            {
                var ecb = _errorCallback; ecb?.Invoke($"摄像头已运行超过 {MaxRunMinutes} 分钟，自动保护释放");
                break;
            }

            IntPtr reader;
            lock (_lock) { reader = _sourceReader; }
            if (reader == IntPtr.Zero) break;

            var hr = ComVtbl.SourceReader_ReadSample(reader,
                MfConst.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                0,
                out _,
                out _,
                out _,
                out var sample);

            if (sample != IntPtr.Zero) ComVtbl.Release(sample);
            if (hr < 0) Log.Debug("Camera: ReadSample = 0x{Code:X8}", hr);

            try { await Task.Delay(CameraPollIntervalMs, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void EnsureMfStarted()
    {
        lock (_lock)
        {
            if (_mfStarted) return;

            var hr = MfNative.MFStartup(MfConst.MF_VERSION);
            Log.Information("Camera: MFStartup = 0x{Code:X8}", hr);
            if (hr < 0) throw new InvalidOperationException($"Media Foundation 初始化失败 (0x{hr:X8})");
            _mfStarted = true;
        }
    }

    private void ShutdownMf()
    {
        lock (_lock)
        {
            if (!_mfStarted) return;
            MfNative.MFShutdown();
            _mfStarted = false;
        }
    }

    private List<string> EnumerateDeviceNames()
    {
        var names = new List<string>();
        EnsureMfStarted();
        var (arr, count) = EnumerateActivates();
        if (arr == IntPtr.Zero) return names;

        try
        {
            for (var i = 0; i < count; i++)
            {
                var activate = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                var name = ReadActivateString(activate, MfConst.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME);
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
        }
        finally
        {
            FreeActivateArray(arr, count);
        }

        return names;
    }

    private unsafe (IntPtr Array, int Count) EnumerateActivates()
    {
        var hr = MfNative.MFCreateAttributes(out var attrs, 1);
        if (hr < 0) throw new InvalidOperationException($"创建摄像头枚举属性失败 (0x{hr:X8})");

        try
        {
            var sourceType = MfConst.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE;
            var videoCapture = MfConst.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            hr = ComVtbl.Attributes_SetGUID(attrs, &sourceType, &videoCapture);
            if (hr < 0) throw new InvalidOperationException($"设置摄像头枚举属性失败 (0x{hr:X8})");

            hr = MfNative.MFEnumDeviceSources(attrs, out var arr, out var count);
            Log.Information("Camera: MFEnumDeviceSources = 0x{Code:X8}, count={Count}", hr, count);
            return hr >= 0 ? (arr, count) : (IntPtr.Zero, 0);
        }
        finally
        {
            ComVtbl.Release(attrs);
        }
    }

    private static unsafe string? ReadActivateString(IntPtr activate, Guid key)
    {
        var localKey = key;
        var hr = ComVtbl.Attributes_GetAllocatedString(activate, &localKey, out var strPtr, out var length);
        if (hr < 0 || strPtr == IntPtr.Zero) return null;

        try
        {
            return Marshal.PtrToStringUni(strPtr, length);
        }
        finally
        {
            MfNative.FreeCoTaskMem(strPtr);
        }
    }

    private static unsafe int ActivateDevice(IntPtr activate, Guid riid, out IntPtr ppv)
    {
        var localRiid = riid;
        return ComVtbl.Activate_ActivateObject(activate, &localRiid, out ppv);
    }

    private static void FreeActivateArray(IntPtr arr, int count)
    {
        if (arr == IntPtr.Zero) return;

        for (var i = 0; i < count; i++)
        {
            var activate = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
            if (activate != IntPtr.Zero) ComVtbl.Release(activate);
        }

        MfNative.FreeCoTaskMem(arr);
    }

    private void StopDevice()
    {
        IntPtr reader;
        IntPtr source;

        lock (_lock)
        {
            reader = _sourceReader;
            source = _mediaSource;
            _sourceReader = IntPtr.Zero;
            _mediaSource = IntPtr.Zero;
        }

        if (reader != IntPtr.Zero) ComVtbl.Release(reader);
        if (source != IntPtr.Zero) ComVtbl.Release(source);
        ShutdownMf();
    }
}
