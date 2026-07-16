import AppKit
import SwiftUI

@main
struct LumenPomodoroMacApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var viewModel = AppViewModel()

    var body: some Scene {
        WindowGroup {
            ContentView(viewModel: viewModel)
                .onAppear {
                    appDelegate.viewModel = viewModel
                    NSApplication.shared.activate(ignoringOtherApps: true)
                }
        }

        MenuBarExtra("Lumen Pomodoro", systemImage: "timer") {
            menuBarContent
        }
        .menuBarExtraStyle(.window)
    }

    @ViewBuilder
    private var menuBarContent: some View {
        if viewModel.settings.menuBarEnabled {
            VStack(alignment: .leading, spacing: 8) {
                Text(viewModel.remainingTime)
                    .font(.headline)
                Text(statusLabel)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Divider()
                Button(viewModel.currentStatus == .idle ? "开始专注" : "暂停/继续") {
                    viewModel.togglePause()
                }
                Button("重置") { viewModel.resetTimer() }
                Divider()
                Button("退出") {
                    if viewModel.confirmExitIfNeeded() {
                        NSApplication.shared.terminate(nil)
                    }
                }
            }
            .padding(8)
        } else {
            Text("菜单栏图标已在设置中关闭")
                .font(.caption)
                .padding(8)
        }
    }

    private var statusLabel: String {
        switch viewModel.currentStatus {
        case .idle: return "准备开始"
        case .focus: return "专注中"
        case .break: return "休息中"
        case .paused: return "已暂停"
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var viewModel: AppViewModel?

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard let viewModel else { return .terminateNow }
        if viewModel.confirmExitIfNeeded() {
            return .terminateNow
        }
        return .terminateCancel
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // 有菜单栏时关闭窗口不退出；无菜单栏时关闭即退出（仍走 terminate 确认）
        !(viewModel?.settings.menuBarEnabled ?? true)
    }
}
