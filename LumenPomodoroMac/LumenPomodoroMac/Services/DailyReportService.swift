import LumenPomodoroMacCore
import Foundation

enum DailyReportService {
    static func yesterdayReport(storage: StorageService = .shared) -> DailyReport? {
        let calendar = Calendar.current
        guard let yesterday = calendar.date(byAdding: .day, value: -1, to: calendar.startOfDay(for: Date())) else {
            return nil
        }

        let allCompleted = storage.loadSessions().filter { $0.completed && $0.endTime != nil }
        let yesterdaySessions = allCompleted.filter {
            guard let end = $0.endTime else { return false }
            return calendar.isDate(end, inSameDayAs: yesterday)
        }

        guard !yesterdaySessions.isEmpty else { return nil }

        let mainTask = Dictionary(grouping: yesterdaySessions, by: \.taskName)
            .mapValues { $0.reduce(0) { $0 + $1.focusMinutes } }
            .max(by: { $0.value < $1.value })?.key ?? "未分类"

        let uniqueTasks = Set(yesterdaySessions.map(\.taskName)).count
        let rated = yesterdaySessions.filter { $0.qualityScore > 0 }
        let avgQuality = rated.isEmpty ? 0 : Double(rated.reduce(0) { $0 + $1.qualityScore }) / Double(rated.count)

        let tasks = storage.loadTasks()
        let yesterdayCategories = Set(yesterdaySessions.compactMap { session in
            tasks.first(where: { $0.id == session.taskId }).map {
                $0.category.isEmpty ? $0.name : $0.category
            } ?? session.taskName
        })
        let allCategories = Set(tasks.map { $0.category.isEmpty ? $0.name : $0.category })
        let missed = allCategories.subtracting(yesterdayCategories)
        var categorySuggestion = ""
        if missed.count > 0 && missed.count <= 3 {
            categorySuggestion = "昨天没有学习「\(missed.sorted().joined(separator: "」「"))」，今天可以补上进度"
        }

        return DailyReport(
            date: yesterday,
            completedPomodoros: yesterdaySessions.count,
            totalMinutes: yesterdaySessions.reduce(0) { $0 + $1.focusMinutes },
            mainTask: mainTask,
            streakDays: InsightEngine.calculateStreak(from: allCompleted),
            avgQualityScore: (avgQuality * 10).rounded() / 10,
            uniqueTasksCount: uniqueTasks,
            categorySuggestion: categorySuggestion
        )
    }
}
