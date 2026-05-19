using System;
using System.Runtime.InteropServices;

namespace LumenPomodoro.Interop;

internal static class MfConst
{
    internal static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE =
        new("C60AC5FE-252A-478F-A0EF-BC8FA5F7CAD3");

    internal static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID =
        new("8AC3587A-4AE7-42D8-99E0-0A6013EEF90F");

    internal static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME =
        new("60D0E559-52F8-4FA2-BBCE-AC5127069A3B");

    internal static readonly Guid IID_IMFMediaSource =
        new("279A808D-AEC7-40C8-9C6B-A6B492C78A66");

    internal const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)0xFFFFFFFC);
    internal const uint MF_VERSION = 0x00020070;
}

/// <summary>
/// Minimal COM vtable wrappers used by the Media Foundation camera path.
/// GUID parameters must be passed as pointers because COM REFGUID is a pointer.
/// </summary>
internal static unsafe class ComVtbl
{
    public static int Release(IntPtr pCom)
    {
        if (pCom == IntPtr.Zero) return 0;
        var release = (delegate* unmanaged<IntPtr, int>)(*(IntPtr***)pCom)[2];
        return release(pCom);
    }

    public static int Attributes_SetGUID(IntPtr pAttr, Guid* key, Guid* value)
    {
        // IMFAttributes::SetGUID = slot 24.
        var setGuid = (delegate* unmanaged<IntPtr, Guid*, Guid*, int>)(*(IntPtr***)pAttr)[24];
        return setGuid(pAttr, key, value);
    }

    public static int Attributes_GetAllocatedString(IntPtr pAttr, Guid* key,
        out IntPtr strPtr, out int length)
    {
        IntPtr tmpStr;
        int tmpLen;
        // IMFAttributes::GetAllocatedString = slot 13.
        var getString = (delegate* unmanaged<IntPtr, Guid*, IntPtr*, int*, int>)(*(IntPtr***)pAttr)[13];
        var hr = getString(pAttr, key, &tmpStr, &tmpLen);
        strPtr = tmpStr;
        length = tmpLen;
        return hr;
    }

    public static int Activate_ActivateObject(IntPtr pActivate, Guid* riid, out IntPtr ppv)
    {
        IntPtr tmp;
        // IMFActivate::ActivateObject = slot 33.
        var activate = (delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)(*(IntPtr***)pActivate)[33];
        var hr = activate(pActivate, riid, &tmp);
        ppv = tmp;
        return hr;
    }

    public static int SourceReader_ReadSample(IntPtr pReader, int streamIndex, int controlFlags,
        out int actualStreamIndex, out int streamFlags, out long timestamp, out IntPtr sample)
    {
        int actualIndex;
        int flags;
        long ts;
        IntPtr samplePtr;
        // IMFSourceReader::ReadSample = slot 9.
        var readSample = (delegate* unmanaged<IntPtr, int, int, int*, int*, long*, IntPtr*, int>)
            (*(IntPtr***)pReader)[9];
        var hr = readSample(pReader, streamIndex, controlFlags, &actualIndex, &flags, &ts, &samplePtr);
        actualStreamIndex = actualIndex;
        streamFlags = flags;
        timestamp = ts;
        sample = samplePtr;
        return hr;
    }
}

internal static class MfNative
{
    [DllImport("mfplat.dll")]
    internal static extern int MFStartup(uint version, uint dwFlags = 0);

    [DllImport("mfplat.dll")]
    internal static extern int MFShutdown();

    [DllImport("mfplat.dll")]
    internal static extern int MFCreateAttributes(out IntPtr ppAttributes, int cInitialSize);

    [DllImport("mf.dll")]
    internal static extern int MFEnumDeviceSources(IntPtr pAttributes,
        out IntPtr pppActivate, out int pcCount);

    [DllImport("mfreadwrite.dll")]
    internal static extern int MFCreateSourceReaderFromMediaSource(
        IntPtr pMediaSource, IntPtr pAttributes, out IntPtr ppReader);

    [DllImport("ole32.dll")]
    internal static extern int CoTaskMemFree(IntPtr ptr);

    internal static void FreeCoTaskMem(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero) CoTaskMemFree(ptr);
    }
}
