import AppKit
import ApplicationServices
import AVFoundation
import SwiftUI
import UserNotifications

/// 首次启动三步引导：差异点 → 隐私 → 场景预设 + 权限提示
struct OnboardingView: View {
    @ObservedObject var viewModel: AppViewModel
    @State private var step = 0
    @State private var selectedScene = "standard"
    @State private var cameraAuth: String = "未知"
    @State private var axTrusted = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            content
            Spacer(minLength: 8)
            HStack {
                if step > 0 {
                    Button("上一步") { step -= 1 }
                }
                Spacer()
                if step < 3 {
                    Button(step == 2 ? "下一步：权限" : "下一步") { step += 1 }
                        .buttonStyle(.borderedProminent)
                } else {
                    Button("开始使用") { finish() }
                        .buttonStyle(.borderedProminent)
                }
            }
        }
        .padding(28)
        .frame(width: 480, height: 420)
        .onAppear { refreshPermissions() }
    }

    @ViewBuilder
    private var content: some View {
        switch step {
        case 0:
            stepHeader(title: "欢迎使用 Lumen Pomodoro", subtitle: "第 1 / 4 步")
            Text("传统番茄钟依赖声音、弹窗和系统通知，备考时很容易被静音或淹没。")
                .foregroundStyle(.secondary)
            Text("多数 Mac 在调用摄像头时会点亮绿色指示灯。Lumen 只借用这个物理信号：专注结束 → 灯亮 → 提醒你该休息。")
                .foregroundStyle(.secondary)
            Text("本地优先，不做账号与云同步。")
                .font(.caption)
                .foregroundStyle(.tertiary)
        case 1:
            stepHeader(title: "隐私承诺", subtitle: "第 2 / 4 步")
            Text("摄像头只用来点亮指示灯，不是采集设备：")
            VStack(alignment: .leading, spacing: 4) {
                Text("• 不保存照片")
                Text("• 不录制视频")
                Text("• 不上传摄像头数据")
                Text("• 不展示摄像头画面")
                Text("• 仅本机调用；超时自动释放")
            }
            .foregroundStyle(.primary)
        case 2:
            stepHeader(title: "选择专注场景", subtitle: "第 3 / 4 步 · 之后可在设置更改")
            Picker("场景", selection: $selectedScene) {
                Text("轻松 — 声音/通知为主").tag("light")
                Text("标准 — 灯 + 防走神（推荐）").tag("standard")
                Text("严格专注 — 灯 + 全屏休息").tag("strict")
            }
            .pickerStyle(.radioGroup)
        default:
            stepHeader(title: "系统权限", subtitle: "第 4 / 4 步 · 按需授权")
            Text("以下权限仅在对应功能开启时需要。可稍后在「系统设置」中修改。")
                .font(.caption)
                .foregroundStyle(.secondary)
            permissionRow(title: "摄像头", detail: cameraAuth, actionTitle: "请求摄像头") {
                requestCamera()
            }
            permissionRow(title: "通知", detail: "用于走神与结束提醒", actionTitle: "请求通知") {
                NotificationService.shared.requestAuthorizationIfNeeded()
            }
            permissionRow(
                title: "辅助功能",
                detail: axTrusted ? "已授权（防走神可读窗口标题）" : "未授权时仍可用空闲检测",
                actionTitle: "打开系统设置"
            ) {
                openAccessibilitySettings()
            }
        }
    }

    private func stepHeader(title: String, subtitle: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.title2.bold())
            Text(subtitle).font(.caption).foregroundStyle(.secondary)
        }
    }

    private func permissionRow(title: String, detail: String, actionTitle: String, action: @escaping () -> Void) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.subheadline.weight(.semibold))
                Text(detail).font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            Button(actionTitle, action: action)
                .buttonStyle(.bordered)
        }
        .padding(.vertical, 4)
    }

    private func refreshPermissions() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized: cameraAuth = "已授权"
        case .denied, .restricted: cameraAuth = "已拒绝（请在系统设置中开启）"
        case .notDetermined: cameraAuth = "尚未请求"
        @unknown default: cameraAuth = "未知"
        }
        axTrusted = AXIsProcessTrusted()
    }

    private func requestCamera() {
        AVCaptureDevice.requestAccess(for: .video) { _ in
            DispatchQueue.main.async { refreshPermissions() }
        }
    }

    private func openAccessibilitySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
        // 提示系统弹窗（若未信任）
        let opts = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(opts)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { refreshPermissions() }
    }

    private func finish() {
        viewModel.completeOnboarding(scene: selectedScene)
        dismiss()
    }
}
