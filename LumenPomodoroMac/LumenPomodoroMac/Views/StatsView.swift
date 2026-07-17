import LumenPomodoroMacCore
import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct StatsView: View {
    @ObservedObject var viewModel: AppViewModel
    @State private var exportMessage: String?

    private var sessions: [FocusSession] { StorageService.shared.loadSessions() }
    private var completed: [FocusSession] { InsightEngine.completedSessions(from: sessions) }
    private var rangeStart: Date {
        Calendar.current.date(byAdding: .day, value: -6, to: Calendar.current.startOfDay(for: Date())) ?? Date()
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                HStack {
                    Text("统计").font(.title2.bold())
                    Spacer()
                    exportButtons
                }

                HStack(spacing: 16) {
                    statCard(title: "今日番茄", value: "\(viewModel.todayStats.completedPomodoros)")
                    statCard(title: "今日专注", value: "\(viewModel.todayStats.totalFocusMinutes) 分")
                    statCard(title: "连续天数", value: "\(viewModel.todayStats.currentStreak) 天")
                }

                if viewModel.settings.insightsEnabled {
                    GroupBox("学习洞察") {
                        let insights = InsightEngine.getInsights(from: sessions, tasks: viewModel.tasks)
                        VStack(alignment: .leading, spacing: 10) {
                            ForEach(Array(insights.enumerated()), id: \.offset) { index, insight in
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(insight.title).font(.headline)
                                    Text(insight.description).font(.caption).foregroundStyle(.secondary)
                                    if !insight.actionHint.isEmpty {
                                        Text(insight.actionHint).font(.caption2).foregroundStyle(Color.accentColor)
                                    }
                                }
                                if index < insights.count - 1 { Divider() }
                            }
                        }
                        .padding(4)
                    }
                }

                GroupBox("目标进度") {
                    VStack(spacing: 10) {
                        ForEach(InsightEngine.getGoalProgress(
                            from: sessions,
                            dailyGoalMinutes: viewModel.settings.dailyGoalMinutes,
                            weeklyGoalMinutes: viewModel.settings.weeklyGoalMinutes
                        )) { goal in
                            VStack(alignment: .leading, spacing: 4) {
                                HStack {
                                    Text(goal.label)
                                    Spacer()
                                    Text("\(goal.currentMinutes)/\(goal.targetMinutes) 分")
                                        .foregroundStyle(.secondary)
                                        .font(.caption)
                                }
                                ProgressView(value: goal.progressPercent, total: 100)
                            }
                        }
                    }
                    .padding(4)
                }

                GroupBox("专注热力图") {
                    HeatmapView(days: InsightEngine.getHeatmapData(from: sessions))
                        .padding(4)
                }

                GroupBox("小时分布（近 7 天）") {
                    HourlyChartView(points: InsightEngine.getHourlyDistribution(from: sessions, start: rangeStart, end: Date()))
                        .padding(4)
                }

                GroupBox("任务分布（近 7 天）") {
                    DonutChartView(slices: InsightEngine.getTaskBreakdown(from: sessions, start: rangeStart, end: Date(), tasks: viewModel.tasks))
                        .padding(4)
                }

                GroupBox("周趋势") {
                    WeeklyTrendView(points: InsightEngine.getWeeklyTrend(from: sessions))
                        .padding(4)
                }

                if let exportMessage {
                    Text(exportMessage).font(.caption).foregroundStyle(.secondary)
                }

                Text("数据目录：\(StorageService.shared.dataDirectoryPath)")
                    .font(.caption2)
                    .foregroundStyle(.tertiary)
            }
            .padding(24)
        }
    }

    private var exportButtons: some View {
        HStack(spacing: 8) {
            Button("导出 CSV") { export(format: .csv) }
            Button("导出 JSON") { export(format: .json) }
        }
        .controlSize(.small)
    }

    private func export(format: ExportFormat) {
        let panel = NSSavePanel()
        panel.canCreateDirectories = true
        panel.nameFieldStringValue = format == .csv ? "lumen-sessions.csv" : "lumen-sessions.json"
        panel.allowedContentTypes = [format == .csv ? .commaSeparatedText : .json]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            try ExportService.shared.writeToFile(completed, url: url, format: format)
            exportMessage = "已导出到 \(url.path)"
        } catch {
            exportMessage = "导出失败：\(error.localizedDescription)"
        }
    }

    private func statCard(title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            Text(value).font(.title3.bold())
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))
    }
}
