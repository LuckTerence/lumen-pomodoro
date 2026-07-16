import AppKit
import ApplicationServices
import Foundation

/// 打开 macOS 系统设置相关页（权限诊断用）
enum SystemSettingsOpener {
    static func openCameraPrivacy() {
        // macOS Ventura+ 与旧版路径兼容尝试
        let urls = [
            "x-apple.systempreferences:com.apple.preference.security?Privacy_Camera",
            "x-apple.systempreferences:com.apple.Settings.PrivacySecurity.extension?Privacy_Camera",
            "x-apple.systempreferences:com.apple.preference.security?Privacy"
        ]
        for s in urls {
            if let url = URL(string: s), NSWorkspace.shared.open(url) {
                return
            }
        }
    }

    static func openNotifications() {
        let urls = [
            "x-apple.systempreferences:com.apple.preference.notifications",
            "x-apple.systempreferences:com.apple.Notifications-Settings.extension"
        ]
        for s in urls {
            if let url = URL(string: s), NSWorkspace.shared.open(url) {
                return
            }
        }
    }

    static func openAccessibility(prompt: Bool = true) {
        if prompt {
            let opts = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
            _ = AXIsProcessTrustedWithOptions(opts)
        }
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }
}
