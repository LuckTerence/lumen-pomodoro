using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using LumenPomodoro.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace LumenPomodoro.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private ITrayService? _trayService;
    private readonly PageProvider _pageProvider;
    private DynamicIslandNotificationWindow? _dynamicIslandWindow;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.GetRequiredService<MainViewModel>();
        _pageProvider = new PageProvider(_viewModel);
        DataContext = _viewModel;

        _viewModel.InAppNotificationRequested += (title, message) =>
            Dispatcher.BeginInvoke(() => ShowInAppNotification(title, message));

        if (_viewModel.AppSettings.TrayEnabled)
        {
            _trayService = App.GetRequiredService<ITrayService>();
            _trayService.AttachToWindow(this);

            _viewModel.TrayMenuNeedsUpdate += () =>
                Dispatcher.BeginInvoke(() => _trayService.UpdateMenuState());

            _viewModel.NotificationRequested += (title, message) =>
                Dispatcher.BeginInvoke(() => _trayService.ShowNotification(title, message));
        }

        NavView.SetPageProviderService(_pageProvider);

        Loaded += MainWindow_Loaded;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = true;
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = false;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(TimerPage));
        ShowDailyReportIfNeeded();
    }

    private void ShowDailyReportIfNeeded()
    {
        var settings = _viewModel.AppSettings;
        if (settings.LastReportShownDate == DateTime.Today) return;

        var report = _viewModel.GetYesterdayReport();
        if (report == null) return;

        Dispatcher.BeginInvoke(() =>
        {
            var dialog = new DailyReportDialog(report);
            dialog.ShowDialog();

            settings.LastReportShownDate = DateTime.Today;
            _viewModel.StorageService.SaveSettings(settings);
        });
    }

    public void NavigateToPage(Type pageType)
    {
        NavView.Navigate(pageType);
    }

    public void HandleWake()
    {
        _viewModel.RefreshTimerOnWake();
    }

    private int _currentTabIndex = 0;
    private readonly Type[] _pageTypes = [typeof(TimerPage), typeof(TasksPage), typeof(StatsPage), typeof(SettingsPage)];

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Tab 切换页面
        if (e.Key == Key.Tab)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                // Shift+Tab: 上一个页面
                _currentTabIndex = (_currentTabIndex - 1 + _pageTypes.Length) % _pageTypes.Length;
            }
            else
            {
                // Tab: 下一个页面
                _currentTabIndex = (_currentTabIndex + 1) % _pageTypes.Length;
            }

            NavView.Navigate(_pageTypes[_currentTabIndex]);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowInAppNotification(string title, string message)
    {
        _dynamicIslandWindow ??= new DynamicIslandNotificationWindow();
        _dynamicIslandWindow.ShowNotification(title, message);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var settings = _viewModel.AppSettings;
        if (settings.TrayEnabled && settings.CloseToTray && _trayService != null)
        {
            e.Cancel = true;
            Hide();
            _trayService.ShowNotification("Lumen Pomodoro", "已最小化到托盘");
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _dynamicIslandWindow?.ForceClose();
        _viewModel.Dispose();
        _trayService?.Dispose();
        _pageProvider.Dispose();
        base.OnClosed(e);
    }

    private sealed class PageProvider : INavigationViewPageProvider, IDisposable
    {
        private readonly MainViewModel _viewModel;
        private TimerPage? _timerPage;
        private TasksPage? _tasksPage;
        private StatsPage? _statsPage;
        private SettingsPage? _settingsPage;
        private SettingsViewModel? _settingsViewModel;

        public PageProvider(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? GetPage(Type pageType)
        {
            if (pageType == typeof(TimerPage))
                return _timerPage ??= CreateTimerPage();
            if (pageType == typeof(TasksPage))
            {
                if (_tasksPage != null)
                {
                    _tasksPage.Refresh();
                    return _tasksPage;
                }
                return _tasksPage = CreateTasksPage();
            }
            if (pageType == typeof(StatsPage))
            {
                if (_statsPage != null)
                {
                    _statsPage.Refresh();
                    return _statsPage;
                }
                return _statsPage = CreateStatsPage();
            }
            if (pageType == typeof(SettingsPage))
                return _settingsPage ??= CreateSettingsPage();
            return null;
        }

        private TimerPage CreateTimerPage()
        {
            var page = new TimerPage(_viewModel);
            var mainWindow = (MainWindow?)Application.Current.MainWindow;
            page.RequestTasksPage += () => mainWindow?.NavigateToPage(typeof(TasksPage));
            page.RequestStatsPage += () => mainWindow?.NavigateToPage(typeof(StatsPage));
            return page;
        }

        private TasksPage CreateTasksPage()
        {
            var tasksVM = App.GetRequiredService<TasksViewModel>();
            tasksVM.TasksChanged += () => _viewModel.ReloadTasks();
            tasksVM.TaskSelected += (task) =>
            {
                _viewModel.SelectedTask = task;
                var mainWindow = (MainWindow?)Application.Current.MainWindow;
                mainWindow?.NavigateToPage(typeof(TimerPage));
            };
            return new TasksPage(tasksVM);
        }

        private StatsPage CreateStatsPage()
        {
            return new StatsPage(App.GetRequiredService<StatsViewModel>());
        }

        private SettingsPage CreateSettingsPage()
        {
            _settingsViewModel = App.GetRequiredService<SettingsViewModel>();
            var page = new SettingsPage(_settingsViewModel);
            page.SettingsSaved += () =>
            {
                _viewModel.ReloadSettings();
                _viewModel.RefreshStats();
            };
            return page;
        }

        public void Dispose()
        {
            _settingsViewModel?.Dispose();
        }
    }
}
