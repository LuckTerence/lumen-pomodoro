import Foundation

public final class StorageService {
    public static let shared = StorageService()

    private let fileManager = FileManager.default
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder
    private let appSupportURL: URL
    private let lock = NSLock()
    /// 会话列表内存缓存，避免统计/连胜/洞察反复解码同一 JSON
    private var sessionsCache: [FocusSession]?

    private var settingsURL: URL { appSupportURL.appendingPathComponent("settings.json") }
    private var tasksURL: URL { appSupportURL.appendingPathComponent("tasks.json") }
    private var sessionsURL: URL { appSupportURL.appendingPathComponent("sessions.json") }
    private var schemaVersionURL: URL { appSupportURL.appendingPathComponent("_schema.json") }
    private var dailyPlanURL: URL { appSupportURL.appendingPathComponent("dailyplan.json") }

    /// 当前数据 schema 版本，需与 Windows 端及 docs/cross-platform-contract.md 对齐。
    private let currentSchemaVersion = 2

    /// 默认使用 Application Support/LumenPomodoro；测试可传入临时目录以隔离真实数据。
    public init(baseDirectory: URL? = nil) {
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

        let base = baseDirectory
            ?? fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        appSupportURL = base.appendingPathComponent("LumenPomodoro", isDirectory: true)
        try? fileManager.createDirectory(at: appSupportURL, withIntermediateDirectories: true)

        runMigrations()
    }

    // MARK: - Schema 迁移

    /// 启动时按 docs/cross-platform-contract.md 的迁移规则执行：
    /// 若存储的 schema 版本低于当前版本，按序执行 Vn → Vn+1；
    /// 高于当前版本（未来数据）仅告警、不静默写坏。
    private func runMigrations() {
        let current = getStoredSchemaVersion()

        if current > currentSchemaVersion {
            NSLog("[StorageService] 数据 schema 版本 \(current) 高于当前 \(currentSchemaVersion)，请升级 App（只读降级，不写坏数据）")
            if !fileManager.fileExists(atPath: schemaVersionURL.path) {
                saveSchemaVersion(current)
            }
            return
        }

        guard current < currentSchemaVersion else {
            // 已是最新，确保 _schema.json 存在以便跨端互拷识别
            if !fileManager.fileExists(atPath: schemaVersionURL.path) {
                saveSchemaVersion(currentSchemaVersion)
            }
            return
        }

        NSLog("[StorageService] 执行数据迁移 V\(current) → V\(currentSchemaVersion)")
        // 逐版本递增迁移，保证跨多版本升级（V0→V1→V2…）也能逐步执行，避免新增版本后“迁移死路”。
        for version in (current + 1)...currentSchemaVersion {
            migrateToVersion(version)
        }
        saveSchemaVersion(currentSchemaVersion)
    }

    /// 执行从 V(version-1) 到 V(version) 的迁移步骤。新增 schema 版本时只需在此追加分支，
    /// 并提升 `currentSchemaVersion`；`runMigrations()` 的循环会自动按序执行所有中间步骤。
    private func migrateToVersion(_ version: Int) {
        switch version {
        case 1:
            migrateV0ToV1()
        case 2:
            migrateV1ToV2()
        default:
            NSLog("[StorageService] 未实现 V\(version) 的迁移步骤，已跳过")
        }
    }

    private func getStoredSchemaVersion() -> Int {
        if let data = try? Data(contentsOf: schemaVersionURL),
           let doc = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
           let v = doc["schema_version"] as? Int {
            return v
        }
        // 兼容回退：从 settings.json 的 SchemaVersion 读取
        if let data = try? Data(contentsOf: settingsURL),
           let doc = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
           let v = doc["SchemaVersion"] as? Int {
            return v
        }
        return 0
    }

    private func saveSchemaVersion(_ version: Int) {
        let meta: [String: Any] = [
            "schema_version": version,
            "updated_at": ISO8601DateFormatter().string(from: Date())
        ]
        if let data = try? JSONSerialization.data(withJSONObject: meta, options: .prettyPrinted) {
            try? data.write(to: schemaVersionURL, options: .atomic)
        }
    }

    private func migrateV0ToV1() {
        // V0（无 SchemaVersion 字段）升级到 V1：仅补写版本元数据，无字段破坏性变更。
        // 若 settings.json 存在且缺少 SchemaVersion，补写为 1。
        guard fileManager.fileExists(atPath: settingsURL.path),
              let data = try? Data(contentsOf: settingsURL),
              var doc = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              (doc["SchemaVersion"] as? Int) ?? 0 == 0 else { return }
        doc["SchemaVersion"] = 1
        if let updated = try? JSONSerialization.data(withJSONObject: doc, options: .prettyPrinted) {
            try? updated.write(to: settingsURL, options: .atomic)
        }
    }

    private func migrateV1ToV2() {
        // V1→V2 引入 dailyplan.json（今日计划，峰值时段排程 A2 使用）。
        // 旧端无此文件，初始化一个空的今日计划，避免首次读取时缺文件报错。
        guard !fileManager.fileExists(atPath: dailyPlanURL.path) else { return }
        let plan = DailyPlan(date: Date(), blocks: [])
        save(plan, to: dailyPlanURL)
    }


    public var dataDirectoryPath: String {
        appSupportURL.path
    }

    public func loadSettings() -> Settings {
        load(from: settingsURL, as: Settings.self) ?? Settings()
    }

    public func saveSettings(_ settings: Settings) {
        save(settings, to: settingsURL)
    }

    public func loadTasks() -> [TaskItem] {
        load(from: tasksURL, as: [TaskItem].self) ?? defaultTasks()
    }

    public func saveTasks(_ tasks: [TaskItem]) {
        save(tasks, to: tasksURL)
    }

    public func loadSessions() -> [FocusSession] {
        lock.lock()
        defer { lock.unlock() }
        if let sessionsCache {
            return sessionsCache
        }
        let loaded = load(from: sessionsURL, as: [FocusSession].self) ?? []
        sessionsCache = loaded
        return loaded
    }

    public func saveSessions(_ sessions: [FocusSession]) {
        lock.lock()
        sessionsCache = sessions
        lock.unlock()
        save(sessions, to: sessionsURL)
    }

    public func appendSession(_ session: FocusSession) {
        var sessions = loadSessions()
        sessions.append(session)
        saveSessions(sessions)
    }

    public func updateSession(_ session: FocusSession) {
        var sessions = loadSessions()
        if let index = sessions.firstIndex(where: { $0.id == session.id }) {
            sessions[index] = session
            saveSessions(sessions)
        }
    }

    public func todayStats(on date: Date = Date()) -> DailyStats {
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

    /// 读取今日计划（峰值时段排程 A2）。若文件缺失或存储的日期不是今天，
    /// 返回一个新的「今日」空计划（按日期重置）。
    public func loadDailyPlan() -> DailyPlan {
        let calendar = Calendar.current
        if let plan = load(from: dailyPlanURL, as: DailyPlan.self),
           calendar.isDate(plan.date, inSameDayAs: Date()) {
            return plan
        }
        return DailyPlan(date: Date(), blocks: [])
    }

    /// 写入今日计划；写入前将日期归正为今天，确保「跨天重置」语义。
    public func saveDailyPlan(_ plan: DailyPlan) {
        var plan = plan
        plan.date = Date()
        save(plan, to: dailyPlanURL)
    }

    public func sessionsForLastDays(_ days: Int) -> [FocusSession] {
        let calendar = Calendar.current
        guard let start = calendar.date(byAdding: .day, value: -(days - 1), to: calendar.startOfDay(for: Date())) else {
            return []
        }
        return loadSessions().filter { $0.completed && $0.startTime >= start }
    }

    private func calculateStreak(until date: Date) -> Int {
        let calendar = Calendar.current
        // 只读盘一次；原先在循环内反复 loadSessions 会在长 streak 时 O(n×days) 放大 IO
        let completedDays: Set<Date> = Set(
            loadSessions()
                .filter(\.completed)
                .map { calendar.startOfDay(for: $0.startTime) }
        )

        var streak = 0
        var checkDate = calendar.startOfDay(for: date)

        while true {
            if completedDays.contains(checkDate) {
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
