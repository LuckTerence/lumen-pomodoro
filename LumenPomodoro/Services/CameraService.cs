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
    private volatile bool _isInitializing = false;
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

            await Task.Run(() => InitializeCameraDevice(), _cancellationTokenSource.Token);

            lock (_lockObject)
            {
                _isRunning = true;
            }

            _statusCallback?.Invoke("摄像头提醒中：当前摄像头被用于点亮指示灯，不会保存或上传画面。");

            _cameraTask = KeepCameraActiveAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            lock (_lockObject)
            {
                _isRunning = false;
            }
            _errorCallback?.Invoke($"摄像头打开失败: {ex.Message}");
        }
        finally
        {
            _isInitializing = false;
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
            _cameraDevice = new MediaFoundationCamera(deviceIndex, _errorCallback);
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

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

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
                    if (_cameraDevice != null)
                    {
                        try { _cameraDevice.Stop(); _cameraDevice.Dispose(); } catch { }
                        _cameraDevice = null;
                    }
                    _isRunning = false;
                    _errorCallback?.Invoke($"摄像头已运行超过 {MaxRunMinutes} 分钟，自动保护释放");
                    return;
                }

                if (_cameraDevice != null && !_cameraDevice.IsRunning)
                {
                    try { _cameraDevice.Dispose(); } catch { }
                    _cameraDevice = null;
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

            if (_cachedCameraNames.Count == 0)
            {
                _cachedCameraNames = new List<string> { "默认摄像头" };
                _cachedCameraCount = 1;
            }

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
            if (_cachedCameraCount == 0) _cachedCameraCount = 1;
            return _cachedCameraCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraService] 获取摄像头数量失败: {ex.Message}");
            _cachedCameraCount = 1;
            return _cachedCameraCount;
        }
    }

    public void ClearCache()
    {
        _cachedCameraNames = null;
        _cachedCameraCount = -1;
    }

    private List<CameraInfo> GetAvailableCameraDevices()
    {
        var devices = MediaFoundationCamera.EnumerateDevices();
        if (devices.Count == 0)
        {
            devices.Add(new CameraInfo("默认摄像头", ""));
        }
        return devices;
    }
}

internal class MediaFoundationCamera : IDisposable
{
    private readonly int _deviceIndex;
    private readonly Action<string>? _errorCallback;
    private volatile bool _isRunning;
    private Thread? _captureThread;
    private CancellationTokenSource? _internalToken;

    public bool IsRunning => _isRunning;

    public MediaFoundationCamera(int deviceIndex, Action<string>? errorCallback = null)
    {
        _deviceIndex = deviceIndex;
        _errorCallback = errorCallback;
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

    private static string HResultToString(int hr) => hr switch
    {
        0 => "S_OK",
        unchecked((int)0x80004003) => "E_POINTER (无效指针)",
        unchecked((int)0x80070057) => "E_INVALIDARG (无效参数)",
        unchecked((int)0x80070005) => "E_ACCESSDENIED (摄像头权限被拒绝)",
        unchecked((int)0x80070015) => "E_NOTREADY (设备未就绪)",
        unchecked((int)0x8007001F) => "E_FAIL (设备故障)",
        unchecked((int)0x80070490) => "E_NOTFOUND (未找到设备)",
        unchecked((int)0x80004005) => "E_UNEXPECTED (未预期错误)",
        _ => $"0x{hr:X8}"
    };

    private void CaptureLoop(CancellationToken token)
    {
        IMFSourceReader? sourceReader = null;
        object? mediaSource = null;

        try
        {
            int hr = NativeMethods.MFStartup(0x20070, NativeMethods.MFSTARTUP_LITE);
            if (hr < 0)
            {
                _errorCallback?.Invoke($"Media Foundation 初始化失败: {HResultToString(hr)}");
                return;
            }

            // 枚举所有视频捕获设备
            var devices = EnumerateDeviceActivates();
            if (devices.Count == 0)
            {
                _errorCallback?.Invoke("未找到任何摄像头设备");
                return;
            }

            if (_deviceIndex >= devices.Count)
            {
                _errorCallback?.Invoke($"摄像头索引 {_deviceIndex} 超出范围 (共 {devices.Count} 个)");
                return;
            }

            var activate = devices[_deviceIndex];
            hr = activate.ActivateObject(typeof(IMFMediaSource).GUID, out mediaSource);

            if (hr < 0 || mediaSource == null)
            {
                Marshal.ReleaseComObject(activate);
                if (mediaSource != null) Marshal.ReleaseComObject(mediaSource);
                _errorCallback?.Invoke($"激活摄像头设备失败: {HResultToString(hr)}");
                return;
            }

            var readerAttributes = NativeMethods.CreateAttributes();

            hr = NativeMethods.MFCreateSourceReaderFromMediaSource(mediaSource, readerAttributes, out sourceReader);

            if (readerAttributes != null) Marshal.ReleaseComObject(readerAttributes);
            Marshal.ReleaseComObject(mediaSource);
            mediaSource = null;

            if (hr < 0 || sourceReader == null)
            {
                _errorCallback?.Invoke($"创建摄像头读取器失败: {HResultToString(hr)}");
                return;
            }

            // 设置最低分辨率以减少带宽和 CPU 占用
            SetLowResolutionMediaType(sourceReader);

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

                if (hr < 0)
                {
                    _errorCallback?.Invoke($"摄像头读取帧失败: {HResultToString(hr)}");
                    break;
                }

                if (flags.HasFlag(NativeMethods.MF_SOURCE_READER_FLAG.MF_SOURCE_READER_F_ENDOFSTREAM))
                {
                    break;
                }

                if (flags.HasFlag(NativeMethods.MF_SOURCE_READER_FLAG.MF_SOURCE_READER_F_ERROR))
                {
                    _errorCallback?.Invoke("摄像头流发生错误 (MF_SOURCE_READER_F_ERROR)");
                    break;
                }

                // 仅点亮指示灯，0.2fps 足够，大幅降低 CPU 和 USB 带宽消耗
                token.WaitHandle.WaitOne(5000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaFoundationCamera] CaptureLoop 异常: {ex.Message}");
            _errorCallback?.Invoke($"摄像头运行时异常: {ex.Message}");
        }
        finally
        {
            if (sourceReader != null) Marshal.ReleaseComObject(sourceReader);
            if (mediaSource != null) Marshal.ReleaseComObject(mediaSource);

            try { NativeMethods.MFShutdown(); } catch { }

            _isRunning = false;
        }
    }

    private static void SetLowResolutionMediaType(IMFSourceReader sourceReader)
    {
        try
        {
            // 尝试设置 160x120 或最低可用分辨率
            IntPtr mediaTypePtr = IntPtr.Zero;
            int hr = sourceReader.GetNativeMediaType(
                NativeMethods.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                0,
                out mediaTypePtr);

            if (hr < 0 || mediaTypePtr == IntPtr.Zero) return;

            // 获取 IMFMediaType 接口来修改分辨率
            var mediaType = (IMFMediaType)Marshal.GetObjectForIUnknown(mediaTypePtr);

            // 设置低分辨率
            mediaType.SetUINT32(NativeMethods.MF_MT_FRAME_SIZE, (160u << 32) | 120u);
            mediaType.SetUINT32(NativeMethods.MF_MT_FRAME_RATE, (1u << 32) | 1u);

            hr = sourceReader.SetCurrentMediaType(
                NativeMethods.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                IntPtr.Zero,
                mediaTypePtr);

            Marshal.ReleaseComObject(mediaType);
            Marshal.FreeCoTaskMem(mediaTypePtr);
        }
        catch
        {
            // 设置分辨率失败不影响主流程
        }
    }

    public static List<CameraInfo> EnumerateDevices()
    {
        var devices = new List<CameraInfo>();
        IMFAttributes? attributes = null;
        IntPtr[]? activates = null;
        int count = 0;

        try
        {
            int hr = NativeMethods.MFStartup(0x20070, NativeMethods.MFSTARTUP_LITE);
            if (hr < 0) return devices;

            attributes = NativeMethods.CreateAttributes();
            if (attributes == null) return devices;

            var sourceType = NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            attributes.SetGUID(NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, ref sourceType);

            hr = NativeMethods.MFEnumDeviceSources(attributes, out activates, out count);
            Marshal.ReleaseComObject(attributes);
            attributes = null;

            if (hr < 0 || activates == null || count == 0)
            {
                if (activates != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (activates[i] != IntPtr.Zero)
                            Marshal.Release(activates[i]);
                    }
                }
                try { NativeMethods.MFShutdown(); } catch { }
                return devices;
            }

            for (int i = 0; i < count; i++)
            {
                if (activates[i] == IntPtr.Zero) continue;

                var activate = (IMFActivate)Marshal.GetObjectForIUnknown(activates[i]);
                string? name = null;

                try
                {
                    activate.GetAllocatedString(
                        NativeMethods.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME,
                        out name,
                        out _);
                }
                catch { }

                devices.Add(new CameraInfo(name ?? $"摄像头 {i + 1}", ""));
                Marshal.ReleaseComObject(activate);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaFoundationCamera] 枚举设备失败: {ex.Message}");
        }
        finally
        {
            if (attributes != null)
            {
                try { Marshal.ReleaseComObject(attributes); } catch { }
            }

            activates = null;
            count = 0;

            try { NativeMethods.MFShutdown(); } catch { }
        }

        return devices;
    }

    public static List<IMFActivate> EnumerateDeviceActivates()
    {
        var result = new List<IMFActivate>();
        IMFAttributes? attributes = null;
        IntPtr[]? activates = null;
        int count = 0;

        try
        {
            int hr = NativeMethods.MFStartup(0x20070, NativeMethods.MFSTARTUP_LITE);
            if (hr < 0) return result;

            attributes = NativeMethods.CreateAttributes();
            if (attributes == null) return result;

            var sourceType = NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            attributes.SetGUID(NativeMethods.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, ref sourceType);

            hr = NativeMethods.MFEnumDeviceSources(attributes, out activates, out count);
            Marshal.ReleaseComObject(attributes);
            attributes = null;

            if (hr < 0 || activates == null || count == 0)
            {
                if (activates != null && count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (activates[i] != IntPtr.Zero)
                            Marshal.Release(activates[i]);
                    }
                }
                return result;
            }

            for (int i = 0; i < count; i++)
            {
                if (activates[i] == IntPtr.Zero) continue;
                var activate = (IMFActivate)Marshal.GetObjectForIUnknown(activates[i]);
                result.Add(activate);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaFoundationCamera] 枚举设备失败: {ex.Message}");
        }
        finally
        {
            if (attributes != null)
            {
                try { Marshal.ReleaseComObject(attributes); } catch { }
            }

            activates = null;
            count = 0;
        }

        return result;
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

[ComImport]
[Guid("7FEE9E9A-1A8A-4AE2-A5F2-CD617F27E4B1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFActivate : IMFAttributes
{
    int ActivateObject(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    int ShutdownObject();
    int DetachObject();
}

[ComImport]
[Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaSource
{
    int GetEventObject(out object ppEventQueue);
    int BeginGetEvent(object pCallback, object pUnkState);
    int EndGetEvent(object pResult, out object ppEvent);
    int QueueEvent(int met, ref Guid guidExtendedType, int hrStatus, [MarshalAs(UnmanagedType.Struct)] ref object pvValue);
}

[ComImport]
[Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType : IMFAttributes
{
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
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME = new Guid("A8E065AD-4F9A-4F31-A1D5-AB89D6A0E78E");

    // Media Type attributes for resolution
    public static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652C33D-D6B2-4012-B834-72060849A11D");
    public static readonly Guid MF_MT_FRAME_RATE = new Guid("C459A2E8-3D2C-4E44-B132-FEE5156C7BB0");

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

    [DllImport("mfplat.dll", PreserveSig = true)]
    public static extern int MFEnumDeviceSources(IMFAttributes attributes, out IntPtr[]? pppSourceActivate, out int pcSourceActivate);

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
