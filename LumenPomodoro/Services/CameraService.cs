using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;

namespace LumenPomodoro.Services;

public class CameraService
{
    private volatile bool _isRunning = false;
    private Task? _cameraTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private int _cameraIndex = 0;
    private Action<string>? _statusCallback;
    private Action<string>? _errorCallback;
    private MediaFoundationCamera? _cameraDevice;
    private readonly object _lockObject = new object();
    private DateTime? _startTime;
    private const int MaxRunMinutes = 30;
    private List<string>? _cachedCameraNames;
    private int _cachedCameraCount = -1;

    public bool IsRunning => _isRunning;

    public void Initialize(int cameraIndex, Action<string> statusCallback, Action<string> errorCallback)
    {
        _cameraIndex = cameraIndex;
        _statusCallback = statusCallback;
        _errorCallback = errorCallback;
    }

    public async Task StartCameraAsync()
    {
        if (_isRunning) return;

        _isRunning = true;
        _startTime = DateTime.Now;

        // 先创建新的 CTS，再取消旧的，避免旧 token 在使用中被 Dispose
        var newCts = new CancellationTokenSource();
        var oldCts = _cancellationTokenSource;
        _cancellationTokenSource = newCts;
        oldCts?.Cancel();

        try
        {
            _statusCallback?.Invoke("正在初始化摄像头...");

            await Task.Run(() => InitializeCameraDevice(), _cancellationTokenSource.Token);

            _statusCallback?.Invoke("摄像头提醒中：当前摄像头被用于点亮指示灯，不会保存或上传画面。");

            _cameraTask = KeepCameraActiveAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _errorCallback?.Invoke($"摄像头打开失败: {ex.Message}");
        }
    }

    private async Task InitializeCameraDevice()
    {
        try
        {
            var devices = GetAvailableCameraDevices();
            if (devices.Count == 0)
            {
                throw new InvalidOperationException("未检测到可用的摄像头设备");
            }

            var deviceIndex = Math.Min(_cameraIndex, devices.Count - 1);
            _cameraDevice = new MediaFoundationCamera(devices[deviceIndex].SymbolicLink);
            _cameraDevice.Start();

            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"摄像头初始化失败: {ex.Message}", ex);
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
                Debug.WriteLine($"[CameraService] 停止摄像头任务异常: {ex.Message}");
            }
        }

        StopCameraDevice();

        lock (_lockObject)
        {
            _isRunning = false;
        }
        _statusCallback?.Invoke("摄像头已关闭");
    }

    private void StopCameraDevice()
    {
        lock (_lockObject)
        {
            if (_cameraDevice != null)
            {
                try
                {
                    _cameraDevice.Stop();
                    _cameraDevice.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CameraService] 停止摄像头设备异常: {ex.Message}");
                }
                finally
                {
                    _cameraDevice = null;
                }
            }
        }
    }

    private async Task KeepCameraActiveAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(100, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            lock (_lockObject)
            {
                if (_startTime.HasValue && (DateTime.Now - _startTime.Value).TotalMinutes >= MaxRunMinutes)
                {
                    if (_cameraDevice != null)
                    {
                        try { _cameraDevice.Stop(); } catch { }
                        _cameraDevice = null;
                    }
                    _isRunning = false;
                    _errorCallback?.Invoke($"摄像头已运行超过 {MaxRunMinutes} 分钟，自动保护释放");
                    return;
                }

                if (_cameraDevice != null && !_cameraDevice.IsRunning)
                {
                    _isRunning = false;
                    _errorCallback?.Invoke("摄像头意外断开");
                    return;
                }
            }
        }
    }

    public List<string> GetAvailableCameras()
    {
        if (_cachedCameraNames != null) return _cachedCameraNames;

        try
        {
            var devices = GetAvailableCameraDevices();
            _cachedCameraNames = devices.Select(d => d.Name).ToList();
            _cachedCameraCount = _cachedCameraNames.Count;
            return _cachedCameraNames;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraService] 获取摄像头列表失败: {ex.Message}");
            _cachedCameraNames = new List<string> { "默认摄像头" };
            _cachedCameraCount = 1;
            return _cachedCameraNames;
        }
    }

    public int GetCameraCount()
    {
        if (_cachedCameraCount >= 0) return _cachedCameraCount;

        try
        {
            _cachedCameraCount = GetAvailableCameraDevices().Count;
            return _cachedCameraCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraService] 获取摄像头数量失败: {ex.Message}");
            _cachedCameraCount = 1;
            return _cachedCameraCount;
        }
    }

    private List<CameraInfo> GetAvailableCameraDevices()
    {
        var devices = new List<CameraInfo>();

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Camera' OR PNPClass = 'Image')");

            foreach (var device in searcher.Get())
            {
                using (device)
                {
                    var name = device["Name"]?.ToString() ?? "Unknown Camera";
                    var deviceId = device["DeviceID"]?.ToString() ?? "";

                    var symbolicLink = GetSymbolicLinkFromDeviceId(deviceId);
                    devices.Add(new CameraInfo(name, symbolicLink));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraService] WMI 查询摄像头失败: {ex.Message}");
            devices.Add(new CameraInfo("默认摄像头", ""));
        }

        if (devices.Count == 0)
        {
            devices.Add(new CameraInfo("默认摄像头", ""));
        }

        return devices;
    }

    private static string GetSymbolicLinkFromDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return "";

        var escaped = deviceId.Replace('\\', '#');
        return $@"\\?\{escaped}";
    }
}

internal class MediaFoundationCamera : IDisposable
{
    private readonly string _symbolicLink;
    private volatile bool _isRunning;
    private Thread? _captureThread;
    private CancellationTokenSource? _internalToken;

    public bool IsRunning => _isRunning;

    public MediaFoundationCamera(string symbolicLink)
    {
        _symbolicLink = symbolicLink;
    }

    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _internalToken = new CancellationTokenSource();

        _captureThread = new Thread(() => CaptureLoop(_internalToken.Token));
        _captureThread.IsBackground = true;
        _captureThread.Start();
    }

    private void CaptureLoop(CancellationToken token)
    {
        IMFSourceReader? sourceReader = null;
        object? mediaSource = null;

        try
        {
            int hr = NativeMethods.MFStartup(0x20070, NativeMethods.MFSTARTUP_LITE);
            if (hr < 0) return;

            var sourceAttributes = NativeMethods.CreateAttributes();
            if (sourceAttributes == null) return;

            var sourceType = NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            sourceAttributes.SetGUID(NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, ref sourceType);

            if (!string.IsNullOrEmpty(_symbolicLink))
            {
                sourceAttributes.SetString(NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK, _symbolicLink);
            }

            hr = NativeMethods.MFCreateDeviceSource(sourceAttributes, out mediaSource);
            Marshal.ReleaseComObject(sourceAttributes);

            if (hr < 0 || mediaSource == null)
            {
                if (mediaSource != null) Marshal.ReleaseComObject(mediaSource);
                return;
            }

            var readerAttributes = NativeMethods.CreateAttributes();

            hr = NativeMethods.MFCreateSourceReaderFromMediaSource(mediaSource, readerAttributes, out sourceReader);

            if (readerAttributes != null) Marshal.ReleaseComObject(readerAttributes);
            Marshal.ReleaseComObject(mediaSource);
            mediaSource = null;

            if (hr < 0 || sourceReader == null) return;

            while (!token.IsCancellationRequested)
            {
                int actualStreamIndex;
                NativeMethods.MF_SOURCE_READER_FLAG flags;
                long timestamp;
                IMFSample? sample;

                hr = sourceReader.ReadSample(
                    NativeMethods.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                    0,
                    out actualStreamIndex,
                    out flags,
                    out timestamp,
                    out sample);

                if (sample != null)
                {
                    Marshal.ReleaseComObject(sample);
                }

                if (hr < 0 ||
                    flags.HasFlag(NativeMethods.MF_SOURCE_READER_FLAG.MF_SOURCE_READER_F_ENDOFSTREAM) ||
                    flags.HasFlag(NativeMethods.MF_SOURCE_READER_FLAG.MF_SOURCE_READER_F_ERROR))
                {
                    break;
                }

                // 仅点亮指示灯，1fps 足够，降低 CPU 和 USB 带宽消耗
                token.WaitHandle.WaitOne(1000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaFoundationCamera] CaptureLoop 异常: {ex.Message}");
        }
        finally
        {
            if (sourceReader != null) Marshal.ReleaseComObject(sourceReader);
            if (mediaSource != null) Marshal.ReleaseComObject(mediaSource);

            try { NativeMethods.MFShutdown(); } catch { }

            _isRunning = false;
        }
    }

    public void Stop()
    {
        _internalToken?.Cancel();
        _captureThread?.Join(3000);
        _isRunning = false;
    }

    public void Dispose()
    {
        Stop();
        _internalToken?.Dispose();
        _internalToken = null;
    }
}

internal class CameraInfo
{
    public string Name { get; }
    public string SymbolicLink { get; }

    public CameraInfo(string name, string symbolicLink)
    {
        Name = name;
        SymbolicLink = symbolicLink;
    }
}

[ComImport]
[Guid("70AD9F79-2726-4412-A4BC-019F4C3B5096")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    int GetItem(ref Guid guidKey, [MarshalAs(UnmanagedType.Struct)] out object value);
    int GetItemType(ref Guid guidKey, out int type);
    int CompareItem(ref Guid guidKey, [MarshalAs(UnmanagedType.Struct)] object value, out bool matches);
    int Compare(IMFAttributes theOther, int comparisonType, out bool matches);
    int GetUINT32(ref Guid guidKey, out uint value);
    int GetUINT64(ref Guid guidKey, out ulong value);
    int GetDouble(ref Guid guidKey, out double value);
    int GetGUID(ref Guid guidKey, out Guid value);
    int GetStringLength(ref Guid guidKey, out uint length);
    int GetString(ref Guid guidKey, IntPtr value, uint cchBufSize, out uint cchLength);
    int GetAllocatedString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string value, out uint length);
    int SetItem(ref Guid guidKey, [MarshalAs(UnmanagedType.Struct)] ref object value);
    int DeleteItem(ref Guid guidKey);
    int DeleteAllItems();
    int SetUINT32(ref Guid guidKey, uint value);
    int SetUINT64(ref Guid guidKey, ulong value);
    int SetDouble(ref Guid guidKey, double value);
    int SetGUID(ref Guid guidKey, ref Guid value);
    int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string value);
    int GetBlobSize(ref Guid guidKey, out uint length);
    int GetBlob(ref Guid guidKey, IntPtr buf, uint length, out uint size);
    int GetAllocatedBlob(ref Guid guidKey, out IntPtr data, out uint size);
    int GetBlobAsInterface(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] out object data);
}

[ComImport]
[Guid("E786A256-4515-4C82-9DD1-FE11C9E5D2E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSourceReader
{
    int GetStreamSelection(int dwStreamIndex, out bool pfSelected);
    int SetStreamSelection(int dwStreamIndex, bool fSelected);
    int GetNativeMediaType(int dwStreamIndex, int dwMediaTypeIndex, out IntPtr ppMediaType);
    int GetCurrentMediaType(int dwStreamIndex, out IntPtr ppMediaType);
    int SetCurrentMediaType(int dwStreamIndex, IntPtr pdwReserved, IntPtr pMediaType);
    int SetCurrentMediaTypeNoCopy(int dwStreamIndex, IntPtr pdwReserved, IntPtr pMediaType);
    int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex,
        out NativeMethods.MF_SOURCE_READER_FLAG pdwStreamFlags, out long pllTimestamp, out IMFSample? ppSample);
    int Flush(int dwStreamIndex);
    int GetServiceForStream(int dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    int GetPresentationAttribute(int dwStreamIndex, ref Guid guidAttribute, [MarshalAs(UnmanagedType.Struct)] out object pvarAttribute);
}

[ComImport]
[Guid("4382E0EC-E6A4-4E4B-922A-787EC3225A9B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample
{
    int GetSampleFlags(out int pdwSampleFlags);
    int SetSampleFlags(int dwSampleFlags);
    int GetSampleTime(out long phnsSampleTime);
    int SetSampleTime(long hnsSampleTime);
    int GetSampleDuration(out long phnsSampleDuration);
    int SetSampleDuration(long hnsSampleDuration);
    int GetBufferCount(out int pdwBufferCount);
    int GetBufferByIndex(int dwIndex, [MarshalAs(UnmanagedType.IUnknown)] out object ppBuffer);
    int ConvertToContiguousBuffer([MarshalAs(UnmanagedType.IUnknown)] out object ppBuffer);
    int AddBuffer([MarshalAs(UnmanagedType.IUnknown)] object pBuffer);
    int RemoveBufferByIndex(int dwIndex);
    int RemoveAllBuffers();
    int GetTotalLength(out int pcbTotalLength);
    int CopyToBuffer([MarshalAs(UnmanagedType.IUnknown)] object pBuffer);
}

internal static class NativeMethods
{
    public const int MFSTARTUP_LITE = 1;
    public const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)0xFFFFFFFC);

    public const int S_OK = 0;
    public const int E_POINTER = unchecked((int)0x80004003);
    public const int E_INVALIDARG = unchecked((int)0x80070057);

    [Flags]
    public enum MF_SOURCE_READER_FLAG : uint
    {
        MF_SOURCE_READER_F_ERROR = 0x00000001,
        MF_SOURCE_READER_F_ENDOFSTREAM = 0x00000002,
        MF_SOURCE_READER_F_NEWSTREAM = 0x00000004,
        MF_SOURCE_READER_F_NATIVEMEDIATYPECHANGED = 0x00000010,
        MF_SOURCE_READER_F_DEFAULTMEDIATYPECHANGED = 0x00000020,
        MF_SOURCE_READER_F_STREAMTICK = 0x00000100
    }

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE = new Guid("C60ACD28-1847-44BA-9782-EF1C183E1D5D");
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = new Guid("C60ACD28-1847-44BA-9782-EF1C183E1D5D");
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK = new Guid("A80C8198-4DC6-46C2-9BF8-9C8D6E4F9C3D");

    [DllImport("mfplat.dll", PreserveSig = true)]
    public static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", PreserveSig = true)]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll", PreserveSig = true)]
    public static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfsrcsnk.dll", PreserveSig = true)]
    public static extern int MFCreateDeviceSource(IMFAttributes attributes, out object? mediaSource);

    [DllImport("mfreadwrite.dll", PreserveSig = true)]
    public static extern int MFCreateSourceReaderFromMediaSource(object mediaSource, IMFAttributes? attributes, out IMFSourceReader? sourceReader);

    public static IMFAttributes? CreateAttributes()
    {
        try
        {
            int hr = MFCreateAttributes(out var attributes, 10);
            if (hr == S_OK) return attributes;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
