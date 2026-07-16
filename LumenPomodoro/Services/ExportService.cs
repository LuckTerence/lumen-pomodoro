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
    private const string CsvDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public string ExportToCsv(List<FocusSession> sessions)
    {
        var sb = new StringBuilder((sessions.Count + 1) * EstimatedCharsPerRow);
        sb.AppendLine("Id,TaskId,TaskName,StartTime,EndTime,FocusMinutes,Completed");

        foreach (var s in sessions)
        {
            AppendField(sb, s.Id);
            AppendField(sb, s.TaskId);
            AppendField(sb, s.TaskName);
            AppendField(sb, s.StartTime.ToString(CsvDateTimeFormat));
            AppendField(sb, s.EndTime?.ToString(CsvDateTimeFormat) ?? "");
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

    /// <summary>
    /// 生成自包含的单文件 HTML 报告，内嵌 CSS，可直接浏览器打开分享。
    /// </summary>
    public string ExportToHtml(List<FocusSession> sessions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine("<title>Lumen Pomodoro 报告</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,-apple-system,sans-serif;max-width:800px;margin:40px auto;padding:0 20px;color:#1a1a2e;background:#f5f5f5}");
        sb.AppendLine("h1{color:#1DB954;border-bottom:2px solid #1DB954;padding-bottom:8px}");
        sb.AppendLine("h2{color:#1DB954;margin-top:24px}");
        sb.AppendLine(".card{background:#fff;border-radius:8px;padding:16px;margin:12px 0;box-shadow:0 1px 3px rgba(0,0,0,.1)}");
        sb.AppendLine(".stat{display:inline-block;text-align:center;margin:8px 16px;min-width:80px}");
        sb.AppendLine(".stat-val{font-size:28px;font-weight:700;color:#1DB954}");
        sb.AppendLine(".stat-label{font-size:12px;color:#6b7280}");
        sb.AppendLine("table{width:100%;border-collapse:collapse}");
        sb.AppendLine("th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #e5e7eb}");
        sb.AppendLine("th{background:#1DB954;color:#fff;font-weight:600}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>Lumen Pomodoro 报告</h1>");
        sb.AppendLine($"<p>生成时间: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");

        var completed = sessions.Where(s => s.Completed).ToList();
        sb.AppendLine("<div class=\"card\"><h2>概览</h2>");
        AppendHtmlStat(sb, "总 session", sessions.Count.ToString());
        AppendHtmlStat(sb, "已完成", completed.Count.ToString());
        AppendHtmlStat(sb, "总专注分钟", completed.Sum(s => s.FocusMinutes).ToString());
        AppendHtmlStat(sb, "平均评分", completed.Where(s => s.QualityScore > 0).Select(s => (double)s.QualityScore).DefaultIfEmpty(0).Average().ToString("F1"));
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"card\"><h2>Session 列表</h2><table>");
        sb.AppendLine("<tr><th>任务</th><th>开始</th><th>结束</th><th>分钟</th><th>评分</th></tr>");
        foreach (var s in sessions.OrderByDescending(s => s.StartTime))
        {
            sb.AppendLine($"<tr><td>{EscapeHtml(s.TaskName)}</td><td>{s.StartTime:yyyy-MM-dd HH:mm}</td><td>{s.EndTime:yyyy-MM-dd HH:mm}</td><td>{s.FocusMinutes}</td><td>{s.QualityScore}</td></tr>");
        }
        sb.AppendLine("</table></div></body></html>");
        return sb.ToString();
    }

    public void ExportToFile(List<FocusSession> sessions, string filePath, ExportFormat format)
    {
        var content = format switch
        {
            ExportFormat.Csv => ExportToCsv(sessions),
            ExportFormat.Html => ExportToHtml(sessions),
            _ => ExportToJson(sessions)
        };
        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static void AppendHtmlStat(StringBuilder sb, string label, string value)
    {
        sb.AppendLine($"<div class=\"stat\"><div class=\"stat-val\">{EscapeHtml(value)}</div><div class=\"stat-label\">{EscapeHtml(label)}</div></div>");
    }

    private static string EscapeHtml(string s) => System.Net.WebUtility.HtmlEncode(s);

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
