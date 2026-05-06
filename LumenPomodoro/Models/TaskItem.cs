namespace LumenPomodoro.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#3B82F6";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class TaskCategories
{
    public static readonly string[] Categories = { "数学", "英语", "政治", "专业课", "其他" };
    
    public static readonly Dictionary<string, string[]> DefaultTasks = new Dictionary<string, string[]>
    {
        { "数学", new[] { "高数", "线代", "概率论", "刷题", "错题复盘" } },
        { "英语", new[] { "单词", "阅读", "翻译", "作文", "真题复盘" } },
        { "政治", new[] { "马原", "史纲", "毛中特", "思修", "选择题" } },
        { "专业课", new[] { "数据结构", "计算机网络", "操作系统", "组成原理" } },
        { "其他", new[] { "复盘", "计划", "背诵", "整理资料" } }
    };
}