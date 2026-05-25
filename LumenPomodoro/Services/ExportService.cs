using System.IO;
using System.Text;
using System.Text.Json;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;

namespace LumenPomodoro.Services;

public class ExportService : IExportService
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    // 预估每行 CSV 字符数：Id(36) + TaskId(36) + TaskName(20) + 2*DateTime(19) + FocusMinutes(3) + Completed(5) + 分隔符(6) + 引号 ≈ 150
    private const int EstimatedCharsPerRow = 150;

    public string ExportToCsv(List<FocusSession> sessions)
    {
        var sb = new StringBuilder((sessions.Count + 1) * EstimatedCharsPerRow);
        sb.AppendLine("Id,TaskId,TaskName,StartTime,EndTime,FocusMinutes,Completed");

        foreach (var s in sessions)
        {
            AppendField(sb, s.Id);
            AppendField(sb, s.TaskId);
            AppendField(sb, s.TaskName);
            AppendField(sb, s.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendField(sb, s.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "");
            sb.Append(s.FocusMinutes);
            sb.Append(',');
            sb.AppendLine(s.Completed ? "true" : "false");
        }

        return sb.ToString();
    }

    public string ExportToJson(List<FocusSession> sessions)
    {
        return JsonSerializer.Serialize(sessions, IndentedOptions);
    }

    public void ExportToFile(List<FocusSession> sessions, string filePath, ExportFormat format)
    {
        var content = format == ExportFormat.Csv
            ? ExportToCsv(sessions)
            : ExportToJson(sessions);

        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static void AppendField(StringBuilder sb, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            sb.Append("\"\",");
            return;
        }

        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n');
        if (!needsQuoting)
        {
            sb.Append(value);
            sb.Append(',');
            return;
        }

        sb.Append('"');
        sb.Append(value.Replace("\"", "\"\""));
        sb.Append("\",");
    }
}
