import AppKit
import ApplicationServices
import AVFoundation
import SwiftUI
import UserNotifications

/// 首次引导：灵动岛 → 本地隐私 → 场景 → 权限
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
        .frame(width: 480, height: 440)
        .onAppear { refreshPermissions() }
    }

    @ViewBuilder
    private var content: some View {
        switch step {
        case 0:
            stepHeader(title: "这就是你的岛", subtitle: "第 1 / 4 步")
            Text("专注时，你几乎不用一直开着主窗口。")
                .foregroundStyle(.secondary)
            Text("屏幕顶部的灵动岛胶囊会显示倒计时；点一下可以暂停、继续或结束休息。切到别的 App，它也还在。")
                .foregroundStyle(.secondary)
            HStack(spacing: 12) {
                Text("专注中").font(.caption.weight(.semibold)).foregroundStyle(.white.opacity(0.9))
                Text("24:18").font(.title3.monospacedDigit().weight(.semibold)).foregroundStyle(.white)
            }
            .padding(.horizontal, 18)
            .padding(.vertical, 12)
            .background(Capsule().fill(Color.black.opacity(0.85)))
            .padding(.top, 8)
            Text("演示：开始专注 → 切到其它窗口 → 看顶部岛继续走时。")
                .font(.caption)
                .foregroundStyle(.tertiary)
        case 1:
            stepHeader(title: "本地优先", subtitle: "第 2 / 4 步")
            Text("数据只存在本机。不做账号，不做云同步。")
            VStack(alignment: .leading, spacing: 4) {
                Text("• 专注记录与设置保存在本地")
                Text("• 默认用灵动岛提醒，不依赖手机")
                Text("• 摄像头灯为可选增强（设置中开启）")
                Text("• 若开启灯：不拍照、不录像、不上传")
            }
            .foregroundStyle(.primary)
        case 2:
            stepHeader(title: "选择专注场景", subtitle: "第 3 / 4 步 · 之后可在设置更改")
            Picker("场景", selection: $selectedScene) {
                Text("轻松 — 岛 + 声音/通知").tag("light")
                Text("标准 — 岛 + 防走神（推荐）").tag("standard")
                Text("严格专注 — 岛 + 全屏休息").tag("strict")
            }
            .pickerStyle(.radioGroup)
            Text("摄像头灯可在设置里按需打开，不是必选。")
                .font(.caption)
                .foregroundStyle(.secondary)
        default:
            stepHeader(title: "系统权限", subtitle: "第 4 / 4 步 · 按需授权")
            Text("以下权限仅在对应功能开启时需要。可稍后在「系统设置」中修改。")
                .font(.caption)
                .foregroundStyle(.secondary)
            permissionRow(title: "通知", detail: "走神与结束提醒（可选）", actionTitle: "请求通知") {
                NotificationService.shared.requestAuthorizationIfNeeded()
            }
            permissionRow(title: "辅助功能", detail: axTrusted ? "已授权（防走神可读窗口标题）" : "未授权时仍可用空闲检测", actionTitle: "打开系统设置") {
                openAccessibilitySettings()
            }
            permissionRow(title: "摄像头", detail: "\(cameraAuth) · 仅当你开启「摄像头灯」时需要", actionTitle: "请求摄像头") {
                requestCamera()
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
        SystemSettingsOpener.openAccessibility(prompt: true)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { refreshPermissions() }
    }

    private func finish() {
        viewModel.completeOnboarding(scene: selectedScene)
        dismiss()
    }
}
