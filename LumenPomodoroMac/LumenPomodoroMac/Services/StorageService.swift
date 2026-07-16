import Foundation

final class StorageService {
    static let shared = StorageService()

    private let fileManager = FileManager.default
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder
    private let appSupportURL: URL

    private var settingsURL: URL { appSupportURL.appendingPathComponent("settings.json") }
    private var tasksURL: URL { appSupportURL.appendingPathComponent("tasks.json") }
    private var sessionsURL: URL { appSupportURL.appendingPathComponent("sessions.json") }

    private init() {
        encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601

        decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let value = try container.decode(String.self)
            if let date = ISO8601DateFormatter().date(from: value) {
                return date
            }
            let formatter = DateFormatter()
            formatter.locale = Locale(identifier: "en_US_POSIX")
            formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
            if let date = formatter.date(from: value) {
                return date
            }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Invalid date: \(value)")
        }

        let base = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        appSupportURL = base.appendingPathComponent("LumenPomodoro", isDirectory: true)
        try? fileManager.createDirectory(at: appSupportURL, withIntermediateDirectories: true)
    }

    var dataDirectoryPath: String {
        appSupportURL.path
    }

    func loadSettings() -> Settings {
        load(from: settingsURL, as: Settings.self) ?? Settings()
    }

    func saveSettings(_ settings: Settings) {
        save(settings, to: settingsURL)
    }

    func loadTasks() -> [TaskItem] {
        load(from: tasksURL, as: [TaskItem].self) ?? defaultTasks()
    }

    func saveTasks(_ tasks: [TaskItem]) {
        save(tasks, to: tasksURL)
    }

    func loadSessions() -> [FocusSession] {
        load(from: sessionsURL, as: [FocusSession].self) ?? []
    }

    func saveSessions(_ sessions: [FocusSession]) {
        save(sessions, to: sessionsURL)
    }

    func appendSession(_ session: FocusSession) {
        var sessions = loadSessions()
        sessions.append(session)
        saveSessions(sessions)
    }

    func updateSession(_ session: FocusSession) {
        var sessions = loadSessions()
        if let index = sessions.firstIndex(where: { $0.id == session.id }) {
            sessions[index] = session
            saveSessions(sessions)
        }
    }

    func todayStats(on date: Date = Date()) -> DailyStats {
        let calendar = Calendar.current
        let sessions = loadSessions().filter {
            $0.completed && calendar.isDate($0.startTime, inSameDayAs: date)
        }

        let pomodoros = sessions.count
        let minutes = sessions.reduce(0) { $0 + $1.focusMinutes }
        let streak = calculateStreak(until: date)

        return DailyStats(
            completedPomodoros: pomodoros,
            totalFocusMinutes: minutes,
            currentStreak: streak
        )
    }

    func sessionsForLastDays(_ days: Int) -> [FocusSession] {
        let calendar = Calendar.current
        guard let start = calendar.date(byAdding: .day, value: -(days - 1), to: calendar.startOfDay(for: Date())) else {
            return []
        }
        return loadSessions().filter { $0.completed && $0.startTime >= start }
    }

    private func calculateStreak(until date: Date) -> Int {
        let calendar = Calendar.current
        var streak = 0
        var checkDate = calendar.startOfDay(for: date)

        while true {
            let hasSession = loadSessions().contains {
                $0.completed && calendar.isDate($0.startTime, inSameDayAs: checkDate)
            }
            if hasSession {
                streak += 1
                guard let previous = calendar.date(byAdding: .day, value: -1, to: checkDate) else { break }
                checkDate = previous
            } else if calendar.isDate(checkDate, inSameDayAs: date) {
                guard let previous = calendar.date(byAdding: .day, value: -1, to: checkDate) else { break }
                checkDate = previous
            } else {
                break
            }
        }
        return streak
    }

    private func defaultTasks() -> [TaskItem] {
        let defaults: [(String, String, String)] = [
            ("数学", "考研", "#3B82F6"),
            ("英语", "考研", "#10B981"),
            ("政治", "考研", "#F59E0B"),
            ("专业课", "考研", "#8B5CF6"),
            ("复盘", "考研", "#EF4444")
        ]
        let tasks = defaults.map { TaskItem(name: $0.0, category: $0.1, color: $0.2) }
        saveTasks(tasks)
        return tasks
    }

    private func load<T: Decodable>(from url: URL, as type: T.Type) -> T? {
        guard fileManager.fileExists(atPath: url.path),
              let data = try? Data(contentsOf: url) else { return nil }
        return try? decoder.decode(T.self, from: data)
    }

    private func save<T: Encodable>(_ value: T, to url: URL) {
        do {
            let data = try encoder.encode(value)
            try data.write(to: url, options: .atomic)
        } catch {
            NSLog("[StorageService] Failed to save \(url.lastPathComponent): \(error.localizedDescription)")
        }
    }
}
