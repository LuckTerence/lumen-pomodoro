import XCTest
import Foundation
import LumenPomodoroMacCore

final class LumenPomodoroMacTests: XCTestCase {

    private func makeTempDir() -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("lumen_mac_test_\(UUID().uuidString)")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: - Settings 编解码

    func testSettingsCodableRoundTrip() throws {
        var s = Settings()
        s.workMinutes = 50
        s.cameraAlertEnabled = true
        s.examName = "托福"
        s.focusGuardBlocklist = ["wechat", "bilibili"]

        let data = try JSONEncoder().encode(s)
        let decoded = try JSONDecoder().decode(Settings.self, from: data)

        XCTAssertEqual(decoded.workMinutes, 50)
        XCTAssertEqual(decoded.cameraAlertEnabled, true)
        XCTAssertEqual(decoded.examName, "托福")
        XCTAssertEqual(decoded.focusGuardBlocklist, ["wechat", "bilibili"])
    }

    // MARK: - StorageService 持久化

    func testStorageServiceSettingsRoundTrip() {
        let tmp = makeTempDir()
        let storage = StorageService(baseDirectory: tmp)

        var s = Settings()
        s.workMinutes = 45
        s.soundEnabled = false
        storage.saveSettings(s)

        // 重新读取（同一目录，模拟重启）
        let reloaded = StorageService(baseDirectory: tmp).loadSettings()
        XCTAssertEqual(reloaded.workMinutes, 45)
        XCTAssertEqual(reloaded.soundEnabled, false)
    }

    func testStorageServiceSessionsRoundTrip() {
        let tmp = makeTempDir()
        let storage = StorageService(baseDirectory: tmp)

        storage.appendSession(FocusSession(
            taskName: "数学", startTime: Date().addingTimeInterval(-1800),
            endTime: Date(), focusMinutes: 25, completed: true))

        let reloaded = StorageService(baseDirectory: tmp).loadSessions()
        XCTAssertEqual(reloaded.count, 1)
        XCTAssertEqual(reloaded.first?.taskName, "数学")
        XCTAssertEqual(reloaded.first?.completed, true)
    }

    // MARK: - Schema 迁移

    func testMigrationV0ToV1() throws {
        let tmp = makeTempDir()
        let appDir = tmp.appendingPathComponent("LumenPomodoro")
        try FileManager.default.createDirectory(at: appDir, withIntermediateDirectories: true)

        // 模拟 V0 数据：settings.json 无 SchemaVersion，且不存在 _schema.json
        let v0: [String: Any] = ["WorkMinutes": 30, "SoundEnabled": true]
        let settingsURL = appDir.appendingPathComponent("settings.json")
        try JSONSerialization.data(withJSONObject: v0, options: .prettyPrinted)
            .write(to: settingsURL, options: .atomic)

        // 触发迁移
        _ = StorageService(baseDirectory: tmp)

        // _schema.json 应已生成且为版本 1
        let schemaURL = appDir.appendingPathComponent("_schema.json")
        XCTAssertTrue(FileManager.default.fileExists(atPath: schemaURL.path))
        let schemaDoc = try JSONSerialization.jsonObject(with: Data(contentsOf: schemaURL)) as? [String: Any]
        XCTAssertEqual(schemaDoc?["schema_version"] as? Int, 1)

        // settings.json 应补写 SchemaVersion = 1
        let settingsDoc = try JSONSerialization.jsonObject(with: Data(contentsOf: settingsURL)) as? [String: Any]
        XCTAssertEqual(settingsDoc?["SchemaVersion"] as? Int, 1)
    }

    // MARK: - InsightEngine

    func testCalculateStreakConsecutiveDays() {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        let sessions = (0..<3).map { offset -> FocusSession in
            let day = calendar.date(byAdding: .day, value: -offset, to: today)!
            let end = calendar.date(bySettingHour: 9, minute: 0, second: 0, of: day)!
            return FocusSession(
                startTime: end.addingTimeInterval(-1500),
                endTime: end, focusMinutes: 25, completed: true)
        }
        XCTAssertEqual(InsightEngine.calculateStreak(from: sessions), 3)
    }

    func testCalculateStreakBrokenByGap() {
        let calendar = Calendar.current
        let today = calendar.startOfDay(for: Date())
        // 今天与两天前，中间缺一天 → 连胜应为 1
        let sessions = [0, 2].map { offset -> FocusSession in
            let day = calendar.date(byAdding: .day, value: -offset, to: today)!
            let end = calendar.date(bySettingHour: 9, minute: 0, second: 0, of: day)!
            return FocusSession(
                startTime: end.addingTimeInterval(-1500),
                endTime: end, focusMinutes: 25, completed: true)
        }
        XCTAssertEqual(InsightEngine.calculateStreak(from: sessions), 1)
    }

    // MARK: - 洞察→行动闭环（A1）

    func testGetInsights_WeakSubject_ReturnsStartFocusAction() {
        let calendar = Calendar.current
        let tasks = [TaskItem(name: "数学"), TaskItem(name: "英语")]
        var sessions: [FocusSession] = []
        // 数学：近 7 天 5 次（日均 5/7 < 1 阈值 → 弱科目）；英语：近 7 天 10 次（不触发）
        for i in 0..<5 {
            let day = calendar.date(byAdding: .day, value: -i, to: Date())!
            let end = calendar.date(bySettingHour: 9, minute: 0, second: 0, of: day)!
            sessions.append(FocusSession(taskName: "数学", startTime: end.addingTimeInterval(-1500), endTime: end, focusMinutes: 25, completed: true))
        }
        for i in 0..<10 {
            let day = calendar.date(byAdding: .day, value: -i, to: Date())!
            let end = calendar.date(bySettingHour: 14, minute: 0, second: 0, of: day)!
            sessions.append(FocusSession(taskName: "英语", startTime: end.addingTimeInterval(-1500), endTime: end, focusMinutes: 25, completed: true))
        }

        let insights = InsightEngine.getInsights(from: sessions, tasks: tasks)
        let actionInsight = insights.first { $0.action?.kind == .startFocus }
        XCTAssertNotNil(actionInsight, "弱科目洞察应返回开始专注动作")
        XCTAssertEqual(actionInsight?.action?.taskName, "数学")
    }

    // MARK: - 峰值时段排程（A2）

    func testGetInsights_PeakHour_ReturnsScheduleBlockAction() {
        let calendar = Calendar.current
        let tasks = [TaskItem(name: "数学"), TaskItem(name: "英语"), TaskItem(name: "政治")]
        var sessions: [FocusSession] = []
        // 数学在 9:00 形成最明显峰值（avgMinutes 最高）
        for i in 0..<5 {
            let day = calendar.date(byAdding: .day, value: -i, to: Date())!
            let end = calendar.date(bySettingHour: 9, minute: 0, second: 0, of: day)!
            sessions.append(FocusSession(taskName: "数学", startTime: end.addingTimeInterval(-1500), endTime: end, focusMinutes: 30, completed: true))
        }
        for i in 0..<4 {
            let day = calendar.date(byAdding: .day, value: -i, to: Date())!
            let end = calendar.date(bySettingHour: 14, minute: 0, second: 0, of: day)!
            sessions.append(FocusSession(taskName: "英语", startTime: end.addingTimeInterval(-1500), endTime: end, focusMinutes: 25, completed: true))
        }
        for i in 0..<3 {
            let day = calendar.date(byAdding: .day, value: -i, to: Date())!
            let end = calendar.date(bySettingHour: 20, minute: 0, second: 0, of: day)!
            sessions.append(FocusSession(taskName: "政治", startTime: end.addingTimeInterval(-1500), endTime: end, focusMinutes: 20, completed: true))
        }

        let insights = InsightEngine.getInsights(from: sessions, tasks: tasks)
        let peak = insights.first { $0.type == .peakHour }
        XCTAssertNotNil(peak, "应生成黄金时段洞察")
        XCTAssertEqual(peak?.action?.kind, .scheduleBlock, "黄金时段洞察应返回 ScheduleBlock 动作")
        XCTAssertEqual(peak?.action?.preferredHour, 9, "preferredHour 应为峰值 9")
        XCTAssertFalse(peak?.action?.taskName.isEmpty ?? true, "taskName 不应为空")
    }

    // MARK: - 动作去重（A3）

    func testSuppressActedActions_HidesScheduleBlock_WhenPlannedToday() {
        let insight = Insight(
            title: "你的黄金时段", description: "", actionHint: "",
            type: .peakHour,
            action: SuggestedAction(kind: .scheduleBlock, actionLabel: "加入今日 9:00", taskName: "数学", preferredHour: 9))
        let plan = DailyPlan(date: Date(), blocks: [PlannedBlock(taskName: "数学", hour: 9)])
        let result = InsightEngine.suppressActedActions([insight], todayPlan: plan, todaysFocusedTaskNames: [])
        XCTAssertNil(result.first?.action, "今日已排程该科目，ScheduleBlock 动作应被隐藏")
    }

    func testSuppressActedActions_HidesStartFocus_WhenFocusedToday() {
        let insight = Insight(
            title: "需要关注", description: "", actionHint: "",
            type: .taskCompletion,
            action: SuggestedAction(kind: .startFocus, actionLabel: "现在专注「数学」", taskName: "数学"))
        let plan = DailyPlan(date: Date(), blocks: [])
        let result = InsightEngine.suppressActedActions([insight], todayPlan: plan, todaysFocusedTaskNames: ["数学"])
        XCTAssertNil(result.first?.action, "今日已专注该科目，StartFocus 动作应被隐藏")
    }

    func testSuppressActedActions_KeepsAction_WhenNotActedToday() {
        let insight = Insight(
            title: "你的黄金时段", description: "", actionHint: "",
            type: .peakHour,
            action: SuggestedAction(kind: .scheduleBlock, actionLabel: "加入今日 9:00", taskName: "数学", preferredHour: 9))
        let plan = DailyPlan(date: Date(), blocks: [])
        let result = InsightEngine.suppressActedActions([insight], todayPlan: plan, todaysFocusedTaskNames: [])
        XCTAssertNotNil(result.first?.action, "今日未达成，动作应保留")
        XCTAssertEqual(result.first?.action?.kind, .scheduleBlock)
    }

    func testDailyPlanSaveAndLoadRoundTrip() throws {
        let tmp = makeTempDir()
        let storage = StorageService(baseDirectory: tmp)

        var plan = storage.loadDailyPlan()
        XCTAssertTrue(plan.blocks.isEmpty, "全新计划应为空")
        plan.blocks.append(PlannedBlock(taskName: "数学", hour: 9, durationMinutes: 25))
        storage.saveDailyPlan(plan)

        let reloaded = storage.loadDailyPlan()
        XCTAssertEqual(reloaded.blocks.count, 1)
        XCTAssertEqual(reloaded.blocks.first?.taskName, "数学")
        XCTAssertEqual(reloaded.blocks.first?.hour, 9)
        XCTAssertEqual(reloaded.blocks.first?.durationMinutes, 25)
    }

    func testMigrationV1CreatesDailyPlan() throws {
        let tmp = makeTempDir()
        let appDir = tmp.appendingPathComponent("LumenPomodoro")
        try FileManager.default.createDirectory(at: appDir, withIntermediateDirectories: true)

        // 模拟 V1 数据：_schema.json 为 1，且无 dailyplan.json
        let meta: [String: Any] = ["schema_version": 1, "updated_at": ISO8601DateFormatter().string(from: Date())]
        try JSONSerialization.data(withJSONObject: meta, options: .prettyPrinted)
            .write(to: appDir.appendingPathComponent("_schema.json"), options: .atomic)
        try? FileManager.default.removeItem(at: appDir.appendingPathComponent("dailyplan.json"))

        _ = StorageService(baseDirectory: tmp)

        let dpURL = appDir.appendingPathComponent("dailyplan.json")
        XCTAssertTrue(FileManager.default.fileExists(atPath: dpURL.path), "迁移 V1→V2 应生成 dailyplan.json")

        let schemaDoc = try JSONSerialization.jsonObject(with: Data(contentsOf: appDir.appendingPathComponent("_schema.json"))) as? [String: Any]
        XCTAssertEqual(schemaDoc?["schema_version"] as? Int, 2)
    }
}
