using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LumenPomodoro.Interop;

/// <summary>
/// Win32 互操作：系统级键鼠空闲时长与前台窗口信息查询。
/// 用于"防走神"前台/空闲检测，全部为只读查询，不修改系统状态。
/// </summary>
internal static class InputMonitorNative
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// 返回距离上次键鼠输入的空闲秒数。查询失败时返回 0（视为有活动）。
    /// </summary>
    public static double GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;

        // Environment.TickCount 与 dwTime 均为无符号毫秒计数，需处理回绕（约 49.7 天）。
        uint now = unchecked((uint)Environment.TickCount);
        uint elapsedMs = unchecked(now - info.dwTime);
        return elapsedMs / 1000.0;
    }

    /// <summary>
    /// 返回当前前台窗口的进程名（不含 .exe）与窗口标题。失败时返回空字符串。
    /// </summary>
    public static (string ProcessName, string WindowTitle) GetForegroundInfo()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return (string.Empty, string.Empty);

        string processName = string.Empty;
        if (GetWindowThreadProcessId(hWnd, out uint pid) != 0 && pid != 0)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                processName = process.ProcessName;
            }
            catch
            {
                // 进程可能已退出或无访问权限，忽略
            }
        }

        return (processName, GetWindowTitle(hWnd));
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length <= 0) return string.Empty;

        var buffer = new StringBuilder(length + 1);
        int copied = GetWindowTextW(hWnd, buffer, buffer.Capacity);
        return copied > 0 ? buffer.ToString() : string.Empty;
    }
}
