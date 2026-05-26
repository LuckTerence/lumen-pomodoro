using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro.Tests.Services;

public class ExportServiceTests
{
    private readonly ExportService _exportService = new();

    [Fact]
    public void ExportToCsv_ProducesCorrectFormat()
    {
        var sessions = new List<FocusSession>
        {
            new() { Id = "1", TaskId = "t1", TaskName = "数学",
                    StartTime = new DateTime(2025, 1, 1, 9, 0, 0),
                    EndTime = new DateTime(2025, 1, 1, 9, 25, 0),
                    FocusMinutes = 25, Completed = true }
        };
        var csv = _exportService.ExportToCsv(sessions);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("TaskName", lines[0]);
        Assert.Contains("数学", lines[1]);
    }

    [Fact]
    public void ExportToCsv_WithEmptyList_ReturnsHeaderOnly()
    {
        var csv = _exportService.ExportToCsv([]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void ExportToJson_ProducesValidJson()
    {
        var sessions = new List<FocusSession>
        {
            new() { Id = "1", TaskName = "数学", Completed = true, FocusMinutes = 25 }
        };
        var json = _exportService.ExportToJson(sessions);
        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FocusSession>>(json);
        Assert.NotNull(parsed);
        Assert.Single(parsed);
    }
}
