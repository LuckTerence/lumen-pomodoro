using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LumenPomodoro.Controls;

/// <summary>
/// 图表控件基类：统一处理主题变更订阅、渲染缓存、脏检测。
/// 子类只需实现 Render() 并在开头调用 SkipIfUnchanged(data)。
/// </summary>
public abstract class ChartBase : UserControl
{
    private object? _lastRenderedData;
    private Size _lastRenderedSize;
    private int _lastThemeHash;

    protected ChartBase()
    {
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += OnThemeChanged;
        Unloaded += (_, _) => Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(Wpf.Ui.Appearance.ApplicationTheme theme, Color accent)
    {
        _lastThemeHash = 0;
        Render();
    }

    /// <summary>
    /// 数据和主题均未变化时跳过重绘。调用方应在 Render() 开头使用：
    /// <code>if (SkipIfUnchanged(data)) return;</code>
    /// </summary>
    protected bool SkipIfUnchanged(object? data)
    {
        if (data == null) return false;

        var currentSize = new Size(ActualWidth, ActualHeight);
        if (currentSize.Width <= 0 || currentSize.Height <= 0) return true;

        var currentThemeHash = Application.Current.TryFindResource("AccentFillColorDefaultBrush")?.GetHashCode() ?? 0;

        if (ReferenceEquals(data, _lastRenderedData)
            && currentSize == _lastRenderedSize
            && currentThemeHash == _lastThemeHash)
        {
            return true;
        }

        _lastRenderedData = data;
        _lastRenderedSize = currentSize;
        _lastThemeHash = currentThemeHash;
        return false;
    }

    /// <summary>
    /// 强制下次 Render 重绘（例如 DP 属性变更时）。
    /// OnDataChanged 回调应调用：InvalidateCache(); Render();
    /// </summary>
    protected void InvalidateCache()
    {
        _lastRenderedData = null;
    }

    protected abstract void Render();
}
