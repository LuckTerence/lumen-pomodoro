import LumenPomodoroMacCore
import SwiftUI

struct SettingsView: View {
    @ObservedObject var viewModel: AppViewModel
    @State private var blocklistText = ""

    var body: some View {
        Form {
            Section {
                Text("主交互是顶栏灵动岛：切窗口也不丢。摄像头灯在底部「高级」中按需开启。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            // MARK: 灵动岛（主交互）
            Section {
                Toggle("启用灵动岛", isOn: settingsBinding(\.dynamicIslandEnabled))
                Text("建议保持开启——这是本产品的主操作入口。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Picker("主窗口在前台时", selection: settingsBinding(\.dynamicIslandWhenFocused)) {
                    Text("淡化缩小").tag("minimize")
                    Text("保持显示").tag("keep")
                    Text("隐藏").tag("hide")
                }
            } header: {
                Text("灵动岛（主交互）")
            } footer: {
                Text("顶栏胶囊显示倒计时；点击可暂停 / 继续 / 选任务。切到其它窗口仍显示。")
            }

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
            }

            Section("目标") {
                Stepper("每日番茄目标 \(viewModel.settings.dailyTargetPomodoros)",
                        value: settingsBinding(\.dailyTargetPomodoros), in: 1...20)
                Stepper("每日专注目标 \(viewModel.settings.dailyGoalMinutes) 分钟",
                        value: settingsBinding(\.dailyGoalMinutes), in: 30...480, step: 30)
                Stepper("每周专注目标 \(viewModel.settings.weeklyGoalMinutes) 分钟",
                        value: settingsBinding(\.weeklyGoalMinutes), in: 120...10080, step: 60)
            }

            Section("可选模块") {
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
                Text("禁止提前结束休息；若已开摄像头灯则禁止手动关灯；完成专注时强制前置窗口。")
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
                    Button("重新查看首次引导") {
                        viewModel.replayOnboarding()
                    }
                    .buttonStyle(.plain)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                }
                .padding(.top, 4)
            }

            // MARK: 高级 · 摄像头灯
            Section {
                Toggle("启用摄像头灯提醒",
                       isOn: settingsBinding(\.cameraAlertEnabled))
                Text("默认关闭。仅在你需要更强休息提示时开启。")
                    .font(.caption).foregroundStyle(.secondary)
                if viewModel.settings.cameraAlertEnabled {
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
                    Button("打开摄像头系统设置") {
                        viewModel.openCameraPrivacySettings()
                    }
                    .buttonStyle(.bordered)
                }
            } header: {
                Text("高级 · 摄像头灯（可选）")
            } footer: {
                Text("非主路径。开启后借用本机摄像头绿色指示灯作硬件提醒；不拍照、不录像、不上传。")
            }
        }
        .formStyle(.grouped)
        .padding()
        .onAppear {
            blocklistText = viewModel.settings.focusGuardBlocklist.joined(separator: "\n")
        }
        .onDisappear {
            // 兜底落盘：所有控件均已通过 settingsBinding 即时保存，
            // 此处再保存一次以覆盖任何遗漏的修改路径，确保设置持久化。
            viewModel.saveSettings()
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
    private func settingsBinding<T>(_ keyPath: WritableKeyPath<LumenPomodoroMacCore.Settings, T>) -> Binding<T> {
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
