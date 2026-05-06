using System.Diagnostics;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.Models;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Services;

public class TrayService
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly MainViewModel _mainViewModel;
    private readonly CameraService _cameraService;
    private readonly StorageService _storageService;

    private Window? _mainWindow;
    private MenuItem _showWindowItem = null!;
    private MenuItem _startPauseItem = null!;
    private MenuItem _stopCameraItem = null!;
    private MenuItem _todayStatsItem = null!;
    private MenuItem _settingsItem = null!;
    private MenuItem _exitItem = null!;

    public TrayService(MainViewModel mainViewModel, CameraService cameraService, StorageService storageService)
    {
        _mainViewModel = mainViewModel;
        _cameraService = cameraService;
        _storageService = storageService;

        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "Lumen Pomodoro",
            Visibility = Visibility.Visible
        };

        CreateMenu();

        _notifyIcon.TrayLeftMouseDown += NotifyIcon_TrayLeftMouseDown;
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

        var settingsWindow = new Views.SettingsWindow(_storageService, _cameraService)
        {
            Owner = _mainWindow
        };
        settingsWindow.ShowDialog();

        _mainViewModel.ReloadSettings();
        _mainViewModel.RefreshStats();
        UpdateMenuState();
    }

    private void ExitItem_Click(object? sender, RoutedEventArgs e)
    {
        FireAndForget(_cameraService.StopCameraAsync(), "退出时停止摄像头");
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
            Debug.WriteLine($"[TrayService] FireAndForget [{operationName}] 异常: {ex.Message}");
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
