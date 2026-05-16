using System.IO;
using System.Text;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using Newtonsoft.Json;

namespace LumenPomodoro.Services;

public class ExportService : IExportService
{
    public string ExportToCsv(List<FocusSession> sessions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,TaskId,TaskName,StartTime,EndTime,FocusMinutes,Completed");

        foreach (var s in sessions)
        {
            sb.AppendLine(string.Join(",",
                Escape(s.Id),
                Escape(s.TaskId),
                Escape(s.TaskName),
                Escape(s.StartTime.ToString("yyyy-MM-dd HH:mm:ss")),
                Escape(s.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""),
                s.FocusMinutes,
                s.Completed));
        }

        return sb.ToString();
    }

    public string ExportToJson(List<FocusSession> sessions)
    {
        return JsonConvert.SerializeObject(sessions, Formatting.Indented);
    }

    public void ExportToFile(List<FocusSession> sessions, string filePath, ExportFormat format)
    {
        var content = format == ExportFormat.Csv
            ? ExportToCsv(sessions)
            : ExportToJson(sessions);

        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
