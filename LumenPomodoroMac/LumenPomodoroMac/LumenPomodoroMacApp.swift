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

        // 菜单栏：倒计时标题 + 快捷操作（Mac 日常主入口）
        MenuBarExtra(isInserted: .constant(true)) {
            menuBarContent
        } label: {
            menuBarLabel
        }
        .menuBarExtraStyle(.menu)
    }

    @ViewBuilder
    private var menuBarLabel: some View {
        if viewModel.settings.menuBarEnabled {
            if viewModel.currentStatus == .focus
                || viewModel.currentStatus == .break
                || viewModel.currentStatus == .paused {
                Text("🍅 \(viewModel.remainingTime)")
            } else {
                Label("Lumen", systemImage: "timer")
            }
        } else {
            // 仍占位，避免 MenuBarExtra 消失导致无法恢复（可在设置打开）
            Image(systemName: "timer")
                .opacity(0.01)
        }
    }

    @ViewBuilder
    private var menuBarContent: some View {
        if viewModel.settings.menuBarEnabled {
            Text(statusLabel)
            Text(viewModel.remainingTime)
                .font(.headline.monospacedDigit())
            if viewModel.settings.cameraAlertEnabled {
                Text(viewModel.cameraStatusDisplay)
                    .font(.caption)
                    .lineLimit(2)
            }
            Divider()
            timerActions
            Divider()
            Button("打开主窗口") { openMainWindow() }
            Menu("场景预设") {
                Button("轻松") { viewModel.applyFocusScenePreset("light") }
                Button("标准") { viewModel.applyFocusScenePreset("standard") }
                Button("严格专注") { viewModel.applyFocusScenePreset("strict") }
            }
            Divider()
            Button("退出") {
                if viewModel.confirmExitIfNeeded() {
                    NSApplication.shared.terminate(nil)
                }
            }
        } else {
            Text("菜单栏图标已在设置中关闭")
            Button("打开主窗口") { openMainWindow() }
            Button("退出") {
                if viewModel.confirmExitIfNeeded() {
                    NSApplication.shared.terminate(nil)
                }
            }
        }
    }

    @ViewBuilder
    private var timerActions: some View {
        if viewModel.currentStatus == .idle && !viewModel.isFocusCompleted {
            Button("开始专注") { viewModel.startFocus() }
        } else if viewModel.currentStatus == .focus || viewModel.currentStatus == .paused {
            Button(viewModel.currentStatus == .paused ? "继续" : "暂停") {
                viewModel.togglePause()
            }
            Button("重置") { viewModel.resetTimer() }
        } else if viewModel.isFocusCompleted {
            Button("短休息") { viewModel.startBreak(long: false) }
            Button("长休息") { viewModel.startBreak(long: true) }
            Button("跳过休息") { viewModel.skipBreak() }
        } else if viewModel.currentStatus == .break {
            if viewModel.settings.effectiveAllowEndBreakEarly {
                Button("结束休息") { viewModel.skipBreak() }
            } else {
                Text("严格模式：请等待休息结束")
            }
        }
    }

    private var statusLabel: String {
        switch viewModel.currentStatus {
        case .idle:
            return viewModel.isFocusCompleted ? "专注完成 · 选择休息" : "准备开始"
        case .focus: return "专注中"
        case .break: return "休息中"
        case .paused: return "已暂停"
        }
    }

    private func openMainWindow() {
        NSApplication.shared.activate(ignoringOtherApps: true)
        for window in NSApplication.shared.windows where window.canBecomeMain {
            window.makeKeyAndOrderFront(nil)
            return
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
        !(viewModel?.settings.menuBarEnabled ?? true)
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            NSApp.activate(ignoringOtherApps: true)
            for window in NSApp.windows where window.canBecomeMain {
                window.makeKeyAndOrderFront(nil)
                break
            }
        }
        return true
    }
}
