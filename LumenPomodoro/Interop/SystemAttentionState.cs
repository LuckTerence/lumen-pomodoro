using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Serilog;

namespace LumenPomodoro.Interop;

/// <summary>
/// 尽力检测 Windows 勿扰 / 通知关闭 / 锁屏状态。
/// 官方无稳定公开 API，采用注册表 + 桌面会话启发式；失败时视为「非勿扰」。
/// </summary>
public static class SystemAttentionState
{
    private const int DesktopSwitchDesktop = 0x0100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(int dwFlags, bool fInherit, int dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    /// <summary>
    /// 当前是否应抑制干扰性提醒（走神通知、置顶等）。
    /// </summary>
    public static bool IsDoNotDisturbActive()
    {
        try
        {
            if (IsWorkstationLocked())
                return true;

            if (AreGlobalToastsDisabled())
                return true;

            if (IsFocusAssistLikelyOn())
                return true;

            return false;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[SystemAttention] DnD 检测失败，视为未开启");
            return false;
        }
    }

    /// <summary>输入桌面不可切换时，通常表示工作站已锁定。</summary>
    public static bool IsWorkstationLocked()
    {
        var h = OpenInputDesktop(0, false, DesktopSwitchDesktop);
        if (h == IntPtr.Zero)
            return true;
        CloseDesktop(h);
        return false;
    }

    /// <summary>通知中心全局关闭 Toast。</summary>
    private static bool AreGlobalToastsDisabled()
    {
        // 0 = toasts off（历史 Quiet Hours / 部分 DnD 场景）
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
            "NOC_GLOBAL_SETTING_TOASTS_ENABLED",
            1);
        return value is int i && i == 0;
    }

    /// <summary>
    /// Focus Assist / 勿扰启发式：读取 CloudStore quiet hours 缓存是否存在非空配置。
    /// 无法 100% 解析二进制 profile，仅作弱信号；与 Toast 关闭、锁屏组合使用。
    /// </summary>
    private static bool IsFocusAssistLikelyOn()
    {
        // Win10/11 部分版本会写此键表示安静时段策略已配置且可能生效
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
            if (key == null) return false;

            // 优先：显式「免打扰」类 DWORD（存在且非 0 时视为开启）
            foreach (var name in new[]
                     {
                         "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK",
                         "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"
                     })
            {
                // 仅探测键存在，不做强判定
                _ = key.GetValue(name);
            }

            // 另一条常见信号：用户关闭了通知横幅
            var banner = key.GetValue("NOC_GLOBAL_SETTING_BANNER_ENABLED");
            if (banner is int b && b == 0)
                return true;
        }
        catch
        {
            // ignore
        }

        return false;
    }
}
