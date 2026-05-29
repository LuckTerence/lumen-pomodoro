using LumenPomodoro.Models;

namespace LumenPomodoro.Services.Abstractions;

public interface IExportService
{
    string ExportToCsv(List<FocusSession> sessions);
    string ExportToJson(List<FocusSession> sessions);
    string ExportToHtml(List<FocusSession> sessions);
    void ExportToFile(List<FocusSession> sessions, string filePath, ExportFormat format);
}

public enum ExportFormat
{
    Csv,
    Json,
    Html
}
