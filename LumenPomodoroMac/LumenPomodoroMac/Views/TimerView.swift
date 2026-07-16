import SwiftUI

struct TimerView: View {
    @ObservedObject var viewModel: AppViewModel

    var body: some View {
        VStack(spacing: 20) {
            taskPicker

            if viewModel.currentStatus == .focus || viewModel.currentStatus == .paused {
                Text("正在专注 · \(viewModel.selectedTask?.name ?? "未知任务")")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            ZStack {
                ArcProgressView(progress: viewModel.progress)
                    .frame(width: 220, height: 220)
                Text(viewModel.remainingTime)
                    .font(.system(size: 56, weight: .light, design: .monospaced))
            }

            controlSection
            cameraStatusSection
            statsFooter
        }
        .padding(24)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    @ViewBuilder
    private var taskPicker: some View {
        if viewModel.currentStatus == .idle && !viewModel.isFocusCompleted {
            Menu {
                ForEach(viewModel.tasks) { task in
                    Button(task.name) { viewModel.selectTask(task) }
                }
            } label: {
                HStack(spacing: 8) {
                    Circle()
                        .fill(Color(hex: viewModel.selectedTask?.color ?? "#6A6A6A"))
                        .frame(width: 8, height: 8)
                    Text(viewModel.selectedTask?.name ?? "选择任务")
                    Image(systemName: "chevron.down")
                        .font(.caption2)
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 8)
                .background(.ultraThinMaterial, in: Capsule())
            }
            .buttonStyle(.plain)
        }
    }

    @ViewBuilder
    private var controlSection: some View {
        switch viewModel.currentStatus {
        case .idle where !viewModel.isFocusCompleted:
            VStack(spacing: 16) {
                Button(action: viewModel.startFocus) {
                    Image(systemName: "play.fill")
                        .font(.title)
                        .frame(width: 64, height: 64)
                        .background(Color.accentColor, in: Circle())
                        .foregroundStyle(.white)
                }
                .buttonStyle(.plain)
                .keyboardShortcut(.space, modifiers: [])

                HStack(spacing: 8) {
                    ForEach(PomodoroPreset.all) { preset in
                        Button(preset.name) { viewModel.applyPreset(preset) }
                            .buttonStyle(.bordered)
                            .controlSize(.small)
                    }
                }

                HStack(spacing: 12) {
                    Button("-") { viewModel.adjustWorkMinutes(by: -5) }
                    Text("\(viewModel.settings.workMinutes) 分钟")
                        .foregroundStyle(.secondary)
                    Button("+") { viewModel.adjustWorkMinutes(by: 5) }
                }
                .buttonStyle(.plain)
            }

        case .idle where viewModel.isFocusCompleted:
            FocusCompletePanel(viewModel: viewModel)

        case .focus:
            HStack(spacing: 12) {
                Button("暂停") { viewModel.togglePause() }
                    .buttonStyle(.borderedProminent)
                Button("重置") { viewModel.resetTimer() }
                    .buttonStyle(.plain)
            }

        case .paused:
            HStack(spacing: 12) {
                Button("继续") { viewModel.togglePause() }
                    .buttonStyle(.borderedProminent)
                Button("重置") { viewModel.resetTimer() }
                    .buttonStyle(.plain)
            }

        case .break:
            if viewModel.settings.effectiveAllowEndBreakEarly {
                Button("结束休息") { viewModel.skipBreak() }
                    .buttonStyle(.borderedProminent)
            } else {
                Text("严格模式：请等待休息结束")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

        default:
            EmptyView()
        }
    }

    @ViewBuilder
    private var cameraStatusSection: some View {
        HStack(spacing: 8) {
            Circle()
                .fill(viewModel.isCameraAlertActive ? Color.orange : Color.secondary.opacity(0.5))
                .frame(width: 6, height: 6)
            Text(viewModel.cameraStatusDisplay)
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.leading)
            if viewModel.isCameraAlertActive && viewModel.settings.effectiveCameraAlertCanManualClose {
                Button("关闭") { viewModel.stopCameraAlert() }
                    .font(.caption)
                    .buttonStyle(.plain)
            }
        }
        .frame(maxWidth: 280)
    }

    private var statsFooter: some View {
        VStack(spacing: 8) {
            ProgressView(value: viewModel.todayPomodoroProgress)
                .progressViewStyle(.linear)
                .frame(width: 220)
            Text("今日 \(viewModel.todayStats.completedPomodoros)/\(viewModel.settings.dailyTargetPomodoros) · 专注 \(viewModel.todayStats.totalFocusMinutes) 分")
                .font(.caption)
                .foregroundStyle(.secondary)

            if let countdown = viewModel.examCountdownText {
                Text(countdown)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Text("空格 开始/暂停 · Esc 重置")
                .font(.caption2)
                .foregroundStyle(.tertiary)
        }
    }
}

private struct FocusCompletePanel: View {
    @ObservedObject var viewModel: AppViewModel

    var body: some View {
        VStack(spacing: 14) {
            Text(viewModel.lastCompletedSummary)
                .font(.subheadline)
                .multilineTextAlignment(.center)

            HStack(spacing: 4) {
                Text("评分")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                ForEach(1...5, id: \.self) { star in
                    Button {
                        viewModel.setRating(star)
                    } label: {
                        Image(systemName: star <= viewModel.userRating ? "star.fill" : "star")
                            .foregroundStyle(.yellow)
                    }
                    .buttonStyle(.plain)
                }
            }

            TextField("专注笔记（可选）", text: $viewModel.currentNotes, axis: .vertical)
                .lineLimit(2...4)
                .textFieldStyle(.roundedBorder)
                .frame(maxWidth: 260)

            Button("开始休息") { viewModel.startBreak(long: false) }
                .buttonStyle(.borderedProminent)

            HStack(spacing: 12) {
                Button(viewModel.suggestLongBreak ? "长休息（推荐）" : "长休息") {
                    viewModel.startBreak(long: true)
                }
                .foregroundStyle(viewModel.suggestLongBreak ? .orange : .secondary)
                Button("跳过") { viewModel.skipBreak() }
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.plain)
            .font(.caption)
        }
    }
}

