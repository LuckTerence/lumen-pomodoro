using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using LumenPomodoro.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace LumenPomodoro.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private ITrayService? _trayService;
    private readonly PageProvider _pageProvider;
    private DynamicIslandNotificationWindow? _dynamicIslandWindow;

    #region DWM 原生亚克力

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSLUCENTSELECTBACKDROP = 3; // Acrylic

    private void EnableAcrylicBackdrop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // 启用深色模式
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // 激活亚克力背景
        int backdropType = DWMSBT_TRANSLUCENTSELECTBACKDROP;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
    }

    #endregion

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.GetRequiredService<MainViewModel>();
        _pageProvider = new PageProvider(_viewModel);
        DataContext = _viewModel;

        _viewModel.InAppNotificationRequested += (title, message) =>
            Dispatcher.BeginInvoke(() => ShowInAppNotification(title, message));

        _viewModel.CountdownStartRequested += (title) =>
            Dispatcher.BeginInvoke(() => StartCountdown(title));
        _viewModel.CountdownUpdateRequested += (time) =>
            Dispatcher.BeginInvoke(() => UpdateCountdown(time));
        _viewModel.CountdownStopRequested += () =>
            Dispatcher.BeginInvoke(() => StopCountdown());

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

        // 监听 Topmost 属性变化
        DependencyPropertyDescriptor.FromProperty(TopmostProperty, typeof(Window))
            .AddValueChanged(this, OnTopmostChanged);
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = true;
        // 激活时增强亚克力效果
        GlassBorder.Opacity = 1.0;
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = false;
        // 失焦时降低透明度
        GlassBorder.Opacity = 0.85;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 激活 DWM 原生亚克力
        EnableAcrylicBackdrop();

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
        var index = Array.IndexOf(_pageTypes, pageType);
        if (index >= 0) _currentTabIndex = index;
    }

    public void HandleWake()
    {
        _viewModel.RefreshTimerOnWake();
    }

    private int _currentTabIndex = 0;
    private readonly Type[] _pageTypes = [typeof(TimerPage), typeof(TasksPage), typeof(StatsPage), typeof(SettingsPage)];

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                _currentTabIndex = (_currentTabIndex - 1 + _pageTypes.Length) % _pageTypes.Length;
            else
                _currentTabIndex = (_currentTabIndex + 1) % _pageTypes.Length;

            NavView.Navigate(_pageTypes[_currentTabIndex]);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnTopmostChanged(object? sender, EventArgs e)
    {
        _viewModel.IsWindowTopmost = Topmost;
        if (Topmost)
            _dynamicIslandWindow?.HideCountdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragWindow(e);
    }

    private void WindowDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFromInteractiveElement(e.OriginalSource as DependencyObject)) return;
        DragWindow(e);
    }

    private void DragWindow(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private static bool IsFromInteractiveElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase ||
                source is TextBoxBase ||
                source is ComboBox ||
                source is Slider ||
                source is NavigationViewItem)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowInAppNotification(string title, string message)
    {
        _dynamicIslandWindow ??= new DynamicIslandNotificationWindow();
        _dynamicIslandWindow.ShowNotification(title, message);
    }

    private void StartCountdown(string title)
    {
        _dynamicIslandWindow ??= new DynamicIslandNotificationWindow();
        _dynamicIslandWindow.StartCountdown(title);
    }

    private void UpdateCountdown(string remainingTime)
    {
        _dynamicIslandWindow?.UpdateCountdown(remainingTime);
    }

    private void StopCountdown()
    {
        _dynamicIslandWindow?.HideCountdown();
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

        public PageProvider(MainViewModel viewModel) => _viewModel = viewModel;

        public object? GetPage(Type pageType)
        {
            if (pageType == typeof(TimerPage))
                return _timerPage ??= CreateTimerPage();
            if (pageType == typeof(TasksPage))
            {
                if (_tasksPage != null) { _tasksPage.Refresh(); return _tasksPage; }
                return _tasksPage = CreateTasksPage();
            }
            if (pageType == typeof(StatsPage))
            {
                if (_statsPage != null) { _statsPage.Refresh(); return _statsPage; }
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

        private StatsPage CreateStatsPage() => new(App.GetRequiredService<StatsViewModel>());

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

        public void Dispose() => _settingsViewModel?.Dispose();
    }
}
