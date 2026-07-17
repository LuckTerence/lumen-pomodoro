import LumenPomodoroMacCore
import SwiftUI

struct HeatmapView: View {
    let days: [HeatmapDay]
    private let columns = Array(repeating: GridItem(.fixed(12), spacing: 3), count: 14)

    var body: some View {
        LazyVGrid(columns: columns, spacing: 3) {
            ForEach(days.suffix(98)) { day in
                RoundedRectangle(cornerRadius: 2)
                    .fill(color(for: day.intensityLevel))
                    .frame(width: 12, height: 12)
                    .help("\(day.date.formatted(date: .abbreviated, time: .omitted)) · \(day.focusMinutes) 分")
            }
        }
    }

    private func color(for level: Int) -> Color {
        switch level {
        case 0: return Color.white.opacity(0.06)
        case 1: return Color.green.opacity(0.25)
        case 2: return Color.green.opacity(0.45)
        case 3: return Color.green.opacity(0.65)
        default: return Color.green.opacity(0.9)
        }
    }
}

struct DonutChartView: View {
    let slices: [TaskSlice]

    var body: some View {
        HStack(spacing: 16) {
            ZStack {
                Circle().stroke(Color.white.opacity(0.08), lineWidth: 16)
                ForEach(Array(slices.enumerated()), id: \.offset) { index, slice in
                    Circle()
                        .trim(from: startAngle(index: index), to: endAngle(index: index))
                        .stroke(Color(hex: slice.taskColor), style: StrokeStyle(lineWidth: 16, lineCap: .butt))
                        .rotationEffect(.degrees(-90))
                }
                if slices.isEmpty {
                    Text("无数据").font(.caption).foregroundStyle(.secondary)
                }
            }
            .frame(width: 100, height: 100)

            VStack(alignment: .leading, spacing: 6) {
                ForEach(slices.prefix(6)) { slice in
                    HStack(spacing: 6) {
                        Circle().fill(Color(hex: slice.taskColor)).frame(width: 8, height: 8)
                        Text(slice.taskName).lineLimit(1)
                        Spacer()
                        Text("\(Int(slice.percentage))%")
                            .foregroundStyle(.secondary)
                            .font(.caption)
                    }
                    .font(.caption)
                }
            }
        }
    }

    private func startAngle(index: Int) -> Double {
        let total = slices.reduce(0.0) { $0 + $1.percentage }
        guard total > 0 else { return 0 }
        let prior = slices.prefix(index).reduce(0.0) { $0 + $1.percentage }
        return prior / 100.0
    }

    private func endAngle(index: Int) -> Double {
        let total = slices.reduce(0.0) { $0 + $1.percentage }
        guard total > 0 else { return 0 }
        let current = slices.prefix(index + 1).reduce(0.0) { $0 + $1.percentage }
        return current / 100.0
    }
}

struct HourlyChartView: View {
    let points: [HourlyDataPoint]
    private var maxValue: Int { max(1, points.map(\.sessionCount).max() ?? 1) }

    var body: some View {
        HStack(alignment: .bottom, spacing: 4) {
            ForEach(points) { point in
                VStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 2)
                        .fill(Color.accentColor.opacity(point.sessionCount > 0 ? 0.85 : 0.15))
                        .frame(width: 10, height: max(4, CGFloat(point.sessionCount) / CGFloat(maxValue) * 60))
                    if point.hour % 3 == 0 {
                        Text("\(point.hour)")
                            .font(.system(size: 8))
                            .foregroundStyle(.tertiary)
                    } else {
                        Text(" ").font(.system(size: 8))
                    }
                }
            }
        }
        .frame(height: 80)
    }
}

struct WeeklyTrendView: View {
    let points: [WeeklyDataPoint]
    private var maxValue: Int { max(1, points.map(\.completedPomodoros).max() ?? 1) }

    var body: some View {
        HStack(alignment: .bottom, spacing: 8) {
            ForEach(points) { point in
                VStack(spacing: 4) {
                    RoundedRectangle(cornerRadius: 3)
                        .fill(Color.accentColor.opacity(0.85))
                        .frame(width: 18, height: max(4, CGFloat(point.completedPomodoros) / CGFloat(maxValue) * 70))
                    Text(point.weekStart, format: .dateTime.month(.twoDigits).day(.twoDigits))
                        .font(.system(size: 8))
                        .foregroundStyle(.tertiary)
                }
            }
        }
        .frame(height: 90)
    }
}
