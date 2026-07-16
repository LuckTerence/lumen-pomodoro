import SwiftUI

struct DailyReportSheet: View {
    let report: DailyReport
    let onDismiss: () -> Void

    var body: some View {
        VStack(spacing: 16) {
            Text("昨日学习报告")
                .font(.title2.bold())
            Text(report.date, format: .dateTime.year().month().day().weekday(.wide))
                .font(.subheadline)
                .foregroundStyle(.secondary)

            HStack(spacing: 0) {
                metricBlock(value: "\(report.completedPomodoros)", label: "番茄钟")
                Divider().frame(height: 48)
                metricBlock(value: "\(report.totalMinutes)", label: "专注分钟")
            }
            .padding()
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))

            VStack(alignment: .leading, spacing: 8) {
                Text("主要学习内容").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                Text(report.mainTask).font(.headline)
                Text("已连续专注 \(report.streakDays) 天 · 完成 \(report.uniqueTasksCount) 个科目")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding()
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 12))

            if report.avgQualityScore > 0 {
                Text("平均质量 \(String(format: "%.1f", report.avgQualityScore)) 星")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if !report.categorySuggestion.isEmpty {
                Text(report.categorySuggestion)
                    .font(.subheadline)
                    .foregroundStyle(Color.accentColor)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal)
            }

            Button("继续学习", action: onDismiss)
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
        }
        .padding(24)
        .frame(width: 380)
    }

    private func metricBlock(value: String, label: String) -> some View {
        VStack(spacing: 6) {
            Text(value).font(.system(size: 36, weight: .semibold, design: .rounded)).foregroundStyle(Color.accentColor)
            Text(label).font(.caption.weight(.semibold)).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity)
    }
}
