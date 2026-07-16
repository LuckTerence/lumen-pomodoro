import ApplicationServices
import Foundation

/// 尽力检测 macOS 专注模式 / 勿扰 / 锁屏。无官方稳定 API，失败视为「非勿扰」。
enum SystemAttentionState {
    /// 当前是否应抑制干扰性提醒
    static func isDoNotDisturbActive() -> Bool {
        if isScreenLocked() { return true }
        if isFocusModeLikelyOn() { return true }
        if isLegacyDoNotDisturbOn() { return true }
        return false
    }

    /// 锁屏（含屏幕保护后锁定）
    static func isScreenLocked() -> Bool {
        guard let dict = CGSessionCopyCurrentDictionary() as? [String: Any] else {
            return false
        }
        if let locked = dict["CGSSessionScreenIsLocked"] as? Bool, locked {
            return true
        }
        // 部分系统用 CFBoolean / NSNumber
        if let num = dict["CGSSessionScreenIsLocked"] as? NSNumber, num.boolValue {
            return true
        }
        return false
    }

    /// Control Center 专注模式菜单项可见 ≈ 有专注模式开启（启发式）
    static func isFocusModeLikelyOn() -> Bool {
        let defaults = UserDefaults(suiteName: "com.apple.controlcenter")
        // 1 = 状态栏显示专注图标，通常表示专注已开
        if defaults?.integer(forKey: "NSStatusItem Visible FocusModes") == 1 {
            return true
        }
        return false
    }

    /// 旧版通知中心勿扰开关
    static func isLegacyDoNotDisturbOn() -> Bool {
        let defaults = UserDefaults(suiteName: "com.apple.notificationcenterui")
        return defaults?.bool(forKey: "doNotDisturb") == true
    }
}
