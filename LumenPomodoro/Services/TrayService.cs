using System.Diagnostics;
using System.Linq;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Serilog;

namespace LumenPomodoro.Services;

public class TrayService : ITrayService
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly MainViewModel _mainViewModel;
    private readonly ICameraService _cameraService;
    private readonly IStorageService _storageService;

    private Window? _mainWindow;
    private MenuItem _showWindowItem = null!;
    private MenuItem _startPauseItem = null!;
    private MenuItem _stopCameraItem = null!;
    private MenuItem _todayStatsItem = null!;
    private MenuItem _settingsItem = null!;
    private MenuItem _exitItem = null!;

    public TrayService(MainViewModel mainViewModel, ICameraService cameraService, IStorageService storageService)
    {
        _mainViewModel = mainViewModel;
        _cameraService = cameraService;
        _storageService = storageService;

        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "Lumen Pomodoro",
            Visibility = Visibility.Visible
        };

        LoadTrayIcon();

        CreateMenu();

        _notifyIcon.TrayLeftMouseDown += NotifyIcon_TrayLeftMouseDown;
    }

    /// <summary>
    /// 使用 IconBitmapDecoder 加载托盘图标，正确处理多尺寸 ICO 文件的各个帧。
    /// 直接从嵌入资源加载并选择合适的尺寸，避免 Hardcodet 库在转换 BitmapImage 到 HICON 时失败。
    /// </summary>
    private void LoadTrayIcon()
    {
        try
        {
            var resourceStream = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (resourceStream == null)
            {
                Log.Warning("托盘图标资源未找到 (pack://application:,,,/app.ico)");
                return;
            }

            var decoder = new IconBitmapDecoder(
                resourceStream.Stream,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);

            // 选择最适合系统托盘的帧：优先 16x16，没有则选最小的 >= 16x16 的帧
            var frame = decoder.Frames
                .OrderBy(f => f.PixelWidth)
                .FirstOrDefault(f => f.PixelWidth >= 16 && f.PixelHeight >= 16)
                ?? decoder.Frames.LastOrDefault();

            if (frame != null)
                _notifyIcon.IconSource = frame;
            else
                Log.Warning("ICO 文件中没有可用的图标帧");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "托盘图标加载失败");
        }
    }

    public void AttachToWindow(Window window)
    {
        _mainWindow = window;
    }

    private void CreateMenu()
    {
        var contextMenu = new ContextMenu();

        _showWindowItem = new MenuItem { Header = "显示主窗口" };
        _showWindowItem.Click += ShowWindowItem_Click;
        contextMenu.Items.Add(_showWindowItem);

        contextMenu.Items.Add(new Separator());

        _startPauseItem = new MenuItem { Header = "开始专注" };
        _startPauseItem.Click += StartPauseItem_Click;
        contextMenu.Items.Add(_startPauseItem);

        _stopCameraItem = new MenuItem { Header = "停止摄像头提醒", IsEnabled = false };
        _stopCameraItem.Click += StopCameraItem_Click;
        contextMenu.Items.Add(_stopCameraItem);

        contextMenu.Items.Add(new Separator());

        _todayStatsItem = new MenuItem { Header = "今日统计" };
        _todayStatsItem.Click += TodayStatsItem_Click;
        contextMenu.Items.Add(_todayStatsItem);

        _settingsItem = new MenuItem { Header = "设置" };
        _settingsItem.Click += SettingsItem_Click;
        contextMenu.Items.Add(_settingsItem);

        contextMenu.Items.Add(new Separator());

        _exitItem = new MenuItem { Header = "退出" };
        _exitItem.Click += ExitItem_Click;
        contextMenu.Items.Add(_exitItem);

        _notifyIcon.ContextMenu = contextMenu;
    }

    public void UpdateMenuState()
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(UpdateMenuState);
            return;
        }

        if (_mainViewModel.CurrentStatus == TimerMode.Idle)
        {
            _startPauseItem.Header = "开始专注";
        }
        else if (_mainViewModel.CurrentStatus == TimerMode.Focus || _mainViewModel.CurrentStatus == TimerMode.Break)
        {
            _startPauseItem.Header = "暂停";
        }
        else if (_mainViewModel.CurrentStatus == TimerMode.Paused)
        {
            _startPauseItem.Header = "继续";
        }

        _stopCameraItem.IsEnabled = _cameraService.IsRunning;
        _todayStatsItem.Header = GetTodayStatsText();
    }

    private string GetTodayStatsText()
    {
        var stats = _storageService.GetTodayStats();
        return $"今日完成: {stats.CompletedPomodoros} 个番茄钟";
    }

    private void NotifyIcon_TrayLeftMouseDown(object? sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void ShowWindowItem_Click(object? sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void StartPauseItem_Click(object? sender, RoutedEventArgs e)
    {
        switch (_mainViewModel.CurrentStatus)
        {
            case TimerMode.Idle:
                _mainViewModel.StartFocus();
                break;
            case TimerMode.Focus:
            case TimerMode.Break:
                _mainViewModel.PauseFocus();
                break;
            case TimerMode.Paused:
                _mainViewModel.ResumeFocus();
                break;
        }
        UpdateMenuState();
    }

    private void StopCameraItem_Click(object? sender, RoutedEventArgs e)
    {
        FireAndForget(_cameraService.StopCameraAsync(), "停止摄像头");
        UpdateMenuState();
    }

    private void TodayStatsItem_Click(object? sender, RoutedEventArgs e)
    {
        var stats = _storageService.GetTodayStats();
        string message = $"今日完成: {stats.CompletedPomodoros} 个番茄钟\n" +
                        $"今日专注时长: {stats.TotalFocusMinutes} 分钟";

        if (stats.TaskStats.Any())
        {
            message += "\n\n任务分布:";
            foreach (var task in stats.TaskStats)
            {
                message += $"\n  {task.Key}: {task.Value} 个";
            }
        }

        MessageBox.Show(message, "今日统计", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SettingsItem_Click(object? sender, RoutedEventArgs e)
    {
        ShowMainWindow();
        if (_mainWindow is Views.MainWindow mw)
            mw.NavigateToPage(typeof(Views.Pages.SettingsPage));
    }

    private void ExitItem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            FireAndForget(_cameraService.StopCameraAsync(), "退出时停止摄像头");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "退出时停止摄像头异常");
        }
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = WindowState.Normal;
        }
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    private static async void FireAndForget(Task task, string operationName)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "FireAndForget [{Operation}] 异常", operationName);
        }
    }

    public void Dispose()
    {
        _showWindowItem.Click -= ShowWindowItem_Click;
        _startPauseItem.Click -= StartPauseItem_Click;
        _stopCameraItem.Click -= StopCameraItem_Click;
        _todayStatsItem.Click -= TodayStatsItem_Click;
        _settingsItem.Click -= SettingsItem_Click;
        _exitItem.Click -= ExitItem_Click;
        _notifyIcon.TrayLeftMouseDown -= NotifyIcon_TrayLeftMouseDown;
        _notifyIcon.Dispose();
    }
}
