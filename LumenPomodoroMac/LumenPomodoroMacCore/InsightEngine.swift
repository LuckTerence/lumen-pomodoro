import Foundation

public enum InsightEngine {
    private static let dayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"]
    private static let minSessionsForInsight = 3
    private static let streakThreshold = 3
    private static let trendChangeThreshold = 0.15
    private static let recentWeeksForTrend = 4
    private static let taskAttentionDays = 7
    private static let taskAttentionAvgThreshold = 1.0
    private static let taskAttentionMinTotal = 5
    private static let maxInsightCount = 5
    private static let milestones = [10, 50, 100, 500, 1000]

    public static func completedSessions(from sessions: [FocusSession]) -> [FocusSession] {
        sessions.filter { $0.completed && $0.endTime != nil }
    }

    public static func getHeatmapData(from sessions: [FocusSession]) -> [HeatmapDay] {
        let completed = completedSessions(from: sessions)
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())

        let earliest = completed.compactMap(\.endTime).map { calendar.startOfDay(for: $0) }.min() ?? today
        let fullRange = calendar.dateComponents([.day], from: earliest, to: today).day ?? 0
        let daysToShow = max(90, min(365, fullRange + 7))
        guard let startDate = calendar.date(byAdding: .day, value: -(daysToShow - 1), to: today) else { return [] }

        var dailyMinutes: [Date: Int] = [:]
        for session in completed {
            guard let end = session.endTime else { continue }
            let day = calendar.startOfDay(for: end)
            guard day >= startDate else { continue }
            dailyMinutes[day, default: 0] += session.focusMinutes
        }

        let maxMinutes = dailyMinutes.values.max() ?? 0
        var result: [HeatmapDay] = []
        for offset in 0..<daysToShow {
            guard let date = calendar.date(byAdding: .day, value: offset, to: startDate) else { continue }
            let minutes = dailyMinutes[date] ?? 0
            let level: Int
            if minutes > 0 && maxMinutes > 0 {
                let ratio = Double(minutes) / Double(maxMinutes)
                if ratio <= 0.25 { level = 1 }
                else if ratio <= 0.5 { level = 2 }
                else if ratio <= 0.75 { level = 3 }
                else { level = 4 }
            } else {
                level = 0
            }
            result.append(HeatmapDay(date: date, focusMinutes: minutes, intensityLevel: level))
        }
        return result
    }

    public static func getHourlyDistribution(from sessions: [FocusSession], start: Date, end: Date) -> [HourlyDataPoint] {
        let calendar = Calendar.current
        let startDay = calendar.startOfDay(for: start)
        let endDay = calendar.startOfDay(for: end)
        let completed = completedSessions(from: sessions).filter {
            guard let endTime = $0.endTime else { return false }
            let day = calendar.startOfDay(for: endTime)
            return day >= startDay && day <= endDay
        }

        var buckets: [Int: (minutes: Int, count: Int)] = [:]
        for session in completed {
            guard let endTime = session.endTime else { continue }
            let hour = calendar.component(.hour, from: endTime)
            var bucket = buckets[hour] ?? (0, 0)
            bucket.minutes += session.focusMinutes
            bucket.count += 1
            buckets[hour] = bucket
        }

        return (0..<24).map { hour in
            let bucket = buckets[hour] ?? (0, 0)
            return HourlyDataPoint(hour: hour, totalMinutes: bucket.minutes, sessionCount: bucket.count)
        }
    }

    public static func getTaskBreakdown(from sessions: [FocusSession], start: Date, end: Date, tasks: [TaskItem]) -> [TaskSlice] {
        let calendar = Calendar.current
        let filtered = completedSessions(from: sessions).filter {
            guard let endTime = $0.endTime else { return false }
            let day = calendar.startOfDay(for: endTime)
            return day >= calendar.startOfDay(for: start) && day <= calendar.startOfDay(for: end)
        }
        guard !filtered.isEmpty else { return [] }

        let colorMap = Dictionary(uniqueKeysWithValues: tasks.map { ($0.name, $0.color) })
        let total = filtered.count
        return Dictionary(grouping: filtered) { $0.taskName.isEmpty ? "未分类" : $0.taskName }
            .map { name, group in
                TaskSlice(
                    taskName: name,
                    taskColor: colorMap[name] ?? "#3B82F6",
                    pomodoroCount: group.count,
                    percentage: (Double(group.count) / Double(total) * 1000).rounded() / 10
                )
            }
            .sorted { $0.pomodoroCount > $1.pomodoroCount }
    }

    public static func getWeeklyTrend(from sessions: [FocusSession]) -> [WeeklyDataPoint] {
        let completed = completedSessions(from: sessions)
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        var thisMonday = monday(for: today, calendar: calendar)
        if thisMonday > today {
            thisMonday = calendar.date(byAdding: .day, value: -7, to: thisMonday) ?? thisMonday
        }

        guard let baselineDate = calendar.date(byAdding: .day, value: -49, to: thisMonday) else { return [] }
        var weekGroups: [Int: [FocusSession]] = [:]
        for session in completed {
            guard let end = session.endTime else { continue }
            let daysFromFirst = calendar.dateComponents([.day], from: baselineDate, to: calendar.startOfDay(for: end)).day ?? -1
            guard daysFromFirst >= 0 else { continue }
            let weekIndex = daysFromFirst / 7
            guard weekIndex >= 0 && weekIndex <= 7 else { continue }
            weekGroups[weekIndex, default: []].append(session)
        }

        return (0...7).reversed().compactMap { i -> WeeklyDataPoint? in
            guard let weekStart = calendar.date(byAdding: .day, value: -7 * i, to: thisMonday) else { return nil }
            let weekSessions = weekGroups[i] ?? []
            return WeeklyDataPoint(
                weekStart: weekStart,
                totalMinutes: weekSessions.reduce(0) { $0 + $1.focusMinutes },
                completedPomodoros: weekSessions.count
            )
        }
    }

    public static func getInsights(from sessions: [FocusSession], tasks: [TaskItem]) -> [Insight] {
        let completed = completedSessions(from: sessions)
        var insights: [Insight] = []

        if completed.count < minSessionsForInsight {
            return [Insight(
                title: "开始你的专注之旅",
                description: "完成更多番茄钟后，这里会为你生成专属的学习洞察。",
                actionHint: "",
                type: .motivation
            )]
        }

        let calendar = Calendar.current
        var hourGroups: [(hour: Int, count: Int, avgMinutes: Double, avgQuality: Double)] = []
        let groupedByHour = Dictionary(grouping: completed) { session -> Int in
            guard let endTime = session.endTime else { return -1 }
            return calendar.component(.hour, from: endTime)
        }
        for (hour, group) in groupedByHour where hour >= 0 {
            guard group.count >= minSessionsForInsight else { continue }
            let avgMinutes = Double(group.reduce(0) { $0 + $1.focusMinutes }) / Double(group.count)
            let rated = group.filter { $0.qualityScore > 0 }
            let avgQuality = rated.isEmpty ? 0 : Double(rated.reduce(0) { $0 + $1.qualityScore }) / Double(rated.count)
            hourGroups.append((hour, group.count, avgMinutes, avgQuality))
        }

        let dayGroups = Dictionary(grouping: completed) { session -> Int in
            guard let endTime = session.endTime else { return -1 }
            return calendar.component(.weekday, from: endTime) - 1
        }
            .map { ($0.key, $0.value.count) }
            .filter { $0.0 >= 0 && $0.1 >= minSessionsForInsight }

        if let bestHour = hourGroups.max(by: { $0.avgMinutes < $1.avgMinutes }) {
            insights.append(Insight(
                title: "你的黄金时段",
                description: "\(bestHour.hour):00 左右是你效率最高的时段，平均每次专注 \(Int(bestHour.avgMinutes)) 分钟。",
                actionHint: "试试把最重要科目安排在这个时段",
                type: .peakHour
            ))
        }

        if let bestDay = dayGroups.max(by: { $0.1 < $1.1 }) {
            let name = dayNames[bestDay.0]
            insights.append(Insight(
                title: "高效日",
                description: "\(name)是你最高效的一天，累计完成 \(bestDay.1) 个番茄钟。",
                actionHint: "",
                type: .bestDay
            ))
        }

        let streak = calculateStreak(from: completed)
        if streak >= streakThreshold {
            insights.append(Insight(
                title: "连续专注",
                description: "你已经连续专注 \(streak) 天了，保持这个节奏！",
                actionHint: "",
                type: .streak
            ))
        }

        let total = completed.count
        if let milestone = milestones.filter({ total >= $0 }).max(), insights.isEmpty {
            insights.append(Insight(
                title: "里程碑达成",
                description: "你已经完成了 \(milestone) 个番茄钟！这是一个了不起的成就。",
                actionHint: "",
                type: .motivation
            ))
        }

        if insights.isEmpty {
            insights.append(Insight(
                title: "稳步前行",
                description: "累计完成 \(total) 个番茄钟，每一天都在进步。",
                actionHint: "",
                type: .motivation
            ))
        }

        return Array(insights.prefix(maxInsightCount))
    }

    public static func getGoalProgress(from sessions: [FocusSession], dailyGoalMinutes: Int, weeklyGoalMinutes: Int) -> [GoalProgress] {
        let completed = completedSessions(from: sessions)
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        var thisMonday = monday(for: today, calendar: calendar)
        if thisMonday > today {
            thisMonday = calendar.date(byAdding: .day, value: -7, to: thisMonday) ?? thisMonday
        }

        var todayMinutes = 0
        var weekMinutes = 0
        for session in completed {
            guard let end = session.endTime else { continue }
            let day = calendar.startOfDay(for: end)
            if day == today { todayMinutes += session.focusMinutes }
            if day >= thisMonday && day <= today { weekMinutes += session.focusMinutes }
        }

        var result: [GoalProgress] = []
        if dailyGoalMinutes > 0 {
            result.append(GoalProgress(
                label: "每日目标",
                currentMinutes: todayMinutes,
                targetMinutes: dailyGoalMinutes,
                progressPercent: min(100, Double(todayMinutes) / Double(dailyGoalMinutes) * 100),
                isCompleted: todayMinutes >= dailyGoalMinutes
            ))
        }
        if weeklyGoalMinutes > 0 {
            result.append(GoalProgress(
                label: "每周目标",
                currentMinutes: weekMinutes,
                targetMinutes: weeklyGoalMinutes,
                progressPercent: min(100, Double(weekMinutes) / Double(weeklyGoalMinutes) * 100),
                isCompleted: weekMinutes >= weeklyGoalMinutes
            ))
        }
        return result
    }

    public static func calculateStreak(from completed: [FocusSession]) -> Int {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        let days = Set(completed.compactMap { $0.endTime.map { calendar.startOfDay(for: $0) } })
            .sorted(by: >)
        guard let first = days.first else { return 0 }
        guard first == today || first == calendar.date(byAdding: .day, value: -1, to: today) else { return 0 }

        var streak = 1
        for i in 1..<days.count {
            guard let expected = calendar.date(byAdding: .day, value: -1, to: days[i - 1]) else { break }
            if days[i] == expected { streak += 1 } else { break }
        }
        return streak
    }

    private static func monday(for date: Date, calendar: Calendar) -> Date {
        let weekday = calendar.component(.weekday, from: date)
        let delta = (weekday + 5) % 7
        return calendar.date(byAdding: .day, value: -delta, to: calendar.startOfDay(for: date)) ?? date
    }
}
