import SwiftUI

struct SettingsView: View {
    @ObservedObject var viewModel: AppViewModel
    @State private var blocklistText = ""

    var body: some View {
        Form {
            Section("专注时长") {
                Stepper("工作 \(viewModel.settings.workMinutes) 分钟",
                        value: settingsBinding(\.workMinutes), in: 5...120, step: 5)
                Stepper("短休息 \(viewModel.settings.shortBreakMinutes) 分钟",
                        value: settingsBinding(\.shortBreakMinutes), in: 1...30)
                Stepper("长休息 \(viewModel.settings.longBreakMinutes) 分钟",
                        value: settingsBinding(\.longBreakMinutes), in: 5...60, step: 5)
                Stepper("每 \(viewModel.settings.longBreakInterval) 个番茄长休息",
                        value: settingsBinding(\.longBreakInterval), in: 2...8)
            }

            Section("摄像头提醒") {
                Toggle("启用摄像头指示灯提醒",
                       isOn: settingsBinding(\.cameraAlertEnabled))
                Picker("提醒模式", selection: settingsBinding(\.cameraAlertMode)) {
                    Text("固定时长").tag(CameraAlertMode.fixedDuration)
                    Text("直到确认").tag(CameraAlertMode.untilConfirm)
                    Text("跟随休息").tag(CameraAlertMode.followBreak)
                }
                if viewModel.settings.cameraAlertMode == .fixedDuration {
                    Stepper("亮灯 \(viewModel.settings.cameraFixedOnSeconds) 秒",
                            value: settingsBinding(\.cameraFixedOnSeconds), in: 30...600, step: 30)
                }
                Toggle("休息期间保持亮灯",
                       isOn: settingsBinding(\.cameraFollowBreakEnabled))
                Toggle("允许手动关闭",
                       isOn: settingsBinding(\.cameraAlertCanManualClose))
                Text("Lumen Pomodoro 不把摄像头当作采集设备使用，只借用 Mac 摄像头绿色指示灯作为本地硬件提醒。")
                    .font(.caption).foregroundStyle(.secondary)
            }

            Section("防走神") {
                Toggle("启用防走神检测",
                       isOn: settingsBinding(\.focusGuardEnabled))
                if viewModel.settings.focusGuardEnabled {
                    Stepper("空闲阈值 \(viewModel.settings.focusGuardIdleSeconds) 秒",
                            value: settingsBinding(\.focusGuardIdleSeconds), in: 30...3600, step: 30)
                    Picker("提醒强度", selection: settingsBinding(\.focusGuardAlertLevel)) {
                        Text("轻").tag(CameraAlertLevel.light)
                        Text("中").tag(CameraAlertLevel.medium)
                        Text("重").tag(CameraAlertLevel.severe)
                    }
                    Stepper("防抖 \(viewModel.settings.focusGuardDebounceHits) 次",
                            value: settingsBinding(\.focusGuardDebounceHits), in: 1...10)
                    Text("连续命中几次后才提醒，减少误报。")
                        .font(.caption).foregroundStyle(.secondary)
                    Stepper("每轮最多提醒 \(viewModel.settings.focusGuardMaxAlertsPerSession) 次",
                            value: settingsBinding(\.focusGuardMaxAlertsPerSession), in: 1...20)
                    Toggle("遵从系统勿扰",
                           isOn: settingsBinding(\.focusGuardRespectDoNotDisturb))
                    Text("锁屏或专注模式开启时不弹走神提醒（仍做检测）。")
                        .font(.caption).foregroundStyle(.secondary)
                    TextField("分心 App 黑名单（每行一个）", text: $blocklistText, axis: .vertical)
                        .lineLimit(4...8)
                    Text("匹配前台 App 名称、Bundle ID 或窗口标题（不区分大小写）。")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }

            Section("提醒") {
                Toggle("声音提醒", isOn: settingsBinding(\.soundEnabled))
                Toggle("弹窗提醒", isOn: settingsBinding(\.popupEnabled))
                Toggle("系统通知", isOn: settingsBinding(\.systemNotificationEnabled))
                Toggle("灵动岛倒计时", isOn: settingsBinding(\.dynamicIslandEnabled))
                Text("窗口不在前台时，在屏幕顶部显示倒计时胶囊。")
                    .font(.caption).foregroundStyle(.secondary)
            }

            Section("目标") {
                Stepper("每日番茄目标 \(viewModel.settings.dailyTargetPomodoros)",
                        value: settingsBinding(\.dailyTargetPomodoros), in: 1...20)
                Stepper("每日专注目标 \(viewModel.settings.dailyGoalMinutes) 分钟",
                        value: settingsBinding(\.dailyGoalMinutes), in: 30...480, step: 30)
                Stepper("每周专注目标 \(viewModel.settings.weeklyGoalMinutes) 分钟",
                        value: settingsBinding(\.weeklyGoalMinutes), in: 120...10080, step: 60)
            }

            Section("功能开关") {
                Toggle("学习洞察", isOn: settingsBinding(\.insightsEnabled))
                Toggle("每日复盘报告", isOn: settingsBinding(\.dailyReportEnabled))
                Toggle("考试倒计时", isOn: settingsBinding(\.examCountdownEnabled))
            }

            Section("考试倒计时") {
                TextField("考试名称", text: settingsBinding(\.examName))
                DatePicker("考试日期", selection: Binding(
                    get: { viewModel.settings.examDate ?? Date() },
                    set: { viewModel.settings.examDate = $0; viewModel.saveSettings() }
                ), displayedComponents: .date)
            }

            Section("系统") {
                Toggle("显示菜单栏图标", isOn: settingsBinding(\.menuBarEnabled))
                Toggle("开机自启", isOn: settingsBinding(\.launchAtLogin))
                Text("开机自启需要应用已签名；开发模式下可能需要在「系统设置 → 通用 → 登录项」手动添加。")
                    .font(.caption).foregroundStyle(.secondary)
                Toggle("计时中退出需确认", isOn: settingsBinding(\.confirmExitWhileFocusing))
                Stepper(
                    "结束前提醒 \(viewModel.settings.sessionEndPreNotifySeconds) 秒",
                    value: settingsBinding(\.sessionEndPreNotifySeconds),
                    in: 0...300,
                    step: 15
                )
                Text("倒计时剩余该秒数时提醒一次；0 为关闭。")
                    .font(.caption).foregroundStyle(.secondary)
                Toggle("全屏休息", isOn: settingsBinding(\.fullscreenBreakEnabled))
                Text("开始休息时覆盖全屏倒计时，减少继续工作的诱惑。")
                    .font(.caption).foregroundStyle(.secondary)
                Toggle("严格模式", isOn: settingsBinding(\.strictModeEnabled))
                Text("禁止手动关摄像头灯、禁止提前结束休息；完成专注时强制前置窗口。")
                    .font(.caption).foregroundStyle(.secondary)

                VStack(alignment: .leading, spacing: 8) {
                    Text("专注场景预设")
                        .font(.subheadline.weight(.semibold))
                    Text("一键切换推荐组合；不改时长与任务。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    HStack(spacing: 8) {
                        Button("轻松") { viewModel.applyFocusScenePreset("light") }
                        Button("标准") { viewModel.applyFocusScenePreset("standard") }
                            .buttonStyle(.borderedProminent)
                        Button("严格专注") { viewModel.applyFocusScenePreset("strict") }
                    }
                }
                .padding(.top, 4)
            }
        }
        .formStyle(.grouped)
        .padding()
        .onAppear {
            blocklistText = viewModel.settings.focusGuardBlocklist.joined(separator: "\n")
        }
        .onChange(of: blocklistText) { _, newValue in
            viewModel.settings.focusGuardBlocklist = newValue
                .split(whereSeparator: \.isNewline)
                .map { String($0).trimmingCharacters(in: .whitespacesAndNewlines) }
                .filter { !$0.isEmpty }
            viewModel.saveSettings()
        }
    }

    /// 创建自动落盘的 Settings 双向绑定（整结构赋值，确保 @Published 与磁盘同步）
    private func settingsBinding<T>(_ keyPath: WritableKeyPath<Settings, T>) -> Binding<T> {
        Binding(
            get: { viewModel.settings[keyPath: keyPath] },
            set: { newValue in
                var next = viewModel.settings
                next[keyPath: keyPath] = newValue
                viewModel.settings = next
                viewModel.saveSettings()
            }
        )
    }
}
