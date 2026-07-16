import Foundation

enum ExportFormat {
    case csv
    case json
}

final class ExportService {
    static let shared = ExportService()

    private let encoder: JSONEncoder = {
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys]
        e.dateEncodingStrategy = .iso8601
        return e
    }()

    private init() {}

    func exportCSV(_ sessions: [FocusSession]) -> String {
        var lines = ["Id,TaskId,TaskName,StartTime,EndTime,FocusMinutes,Completed,QualityScore,Notes"]
        let formatter = ISO8601DateFormatter()
        for session in sessions {
            let fields = [
                csvField(session.id),
                csvField(session.taskId),
                csvField(session.taskName),
                csvField(formatter.string(from: session.startTime)),
                csvField(session.endTime.map { formatter.string(from: $0) } ?? ""),
                String(session.focusMinutes),
                session.completed ? "true" : "false",
                String(session.qualityScore),
                csvField(session.notes ?? "")
            ]
            lines.append(fields.joined(separator: ","))
        }
        return lines.joined(separator: "\n")
    }

    func exportJSON(_ sessions: [FocusSession]) -> String {
        guard let data = try? encoder.encode(sessions),
              let text = String(data: data, encoding: .utf8) else {
            return "[]"
        }
        return text
    }

    func writeToFile(_ sessions: [FocusSession], url: URL, format: ExportFormat) throws {
        let content: String
        switch format {
        case .csv: content = exportCSV(sessions)
        case .json: content = exportJSON(sessions)
        }
        try content.write(to: url, atomically: true, encoding: .utf8)
    }

    private func csvField(_ value: String) -> String {
        if value.contains(",") || value.contains("\"") || value.contains("\n") {
            return "\"\(value.replacingOccurrences(of: "\"", with: "\"\""))\""
        }
        return value
    }
}
