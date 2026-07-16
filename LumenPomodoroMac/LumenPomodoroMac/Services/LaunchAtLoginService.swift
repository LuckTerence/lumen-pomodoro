import Foundation
import ServiceManagement

enum LaunchAtLoginService {
    static func isEnabled() -> Bool {
        if #available(macOS 13.0, *) {
            return SMAppService.mainApp.status == .enabled
        }
        return legacyPlistExists()
    }

    static func setEnabled(_ enabled: Bool) throws {
        if #available(macOS 13.0, *) {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
            return
        }
        try setLegacyLaunchAgent(enabled: enabled)
    }

    private static var legacyPlistURL: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/LaunchAgents/com.luckterence.lumenpomodoro.mac.plist")
    }

    private static func legacyPlistExists() -> Bool {
        FileManager.default.fileExists(atPath: legacyPlistURL.path)
    }

    private static func setLegacyLaunchAgent(enabled: Bool) throws {
        let url = legacyPlistURL
        if enabled {
            guard let exec = Bundle.main.executableURL else { return }
            let plist: [String: Any] = [
                "Label": "com.luckterence.lumenpomodoro.mac",
                "ProgramArguments": [exec.path],
                "RunAtLoad": true
            ]
            let data = try PropertyListSerialization.data(fromPropertyList: plist, format: .xml, options: 0)
            try data.write(to: url)
        } else if FileManager.default.fileExists(atPath: url.path) {
            try FileManager.default.removeItem(at: url)
        }
    }
}
