using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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
    private readonly IServiceProvider _serviceProvider;
    private ITrayService? _trayService;
    private readonly PageProvider _pageProvider;
    private DynamicIslandNotificationWindow? _dynamicIslandWindow;
    private FullscreenBreakWindow? _fullscreenBreakWindow;

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

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int backdropType = DWMSBT_TRANSLUCENTSELECTBACKDROP;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
    }

    #endregion

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _pageProvider = new PageProvider(_viewModel, _serviceProvider);
        DataContext = _viewModel;

        _viewModel.InAppNotificationRequested += (title, message) =>
            Dispatcher.BeginInvoke(() => ShowInAppNotification(title, message));

        _viewModel.CountdownStartRequested += (title) =>
            Dispatcher.BeginInvoke(() => StartCountdown(title));
        _viewModel.CountdownUpdateRequested += (time) =>
            Dispatcher.BeginInvoke(() => UpdateCountdown(time));
        _viewModel.CountdownStopRequested += () =>
            Dispatcher.BeginInvoke(() => StopCountdown());

        _viewModel.FullscreenBreakShowRequested += (title, remaining, allowEnd) =>
            Dispatcher.BeginInvoke(() => ShowFullscreenBreak(title, remaining, allowEnd));
        _viewModel.FullscreenBreakUpdateRequested += (remaining) =>
            Dispatcher.BeginInvoke(() => _fullscreenBreakWindow?.UpdateCountdown(remaining));
        _viewModel.FullscreenBreakHideRequested += () =>
            Dispatcher.BeginInvoke(HideFullscreenBreak);

        _viewModel.IslandTasksChanged += () =>
            Dispatcher.BeginInvoke(SyncIslandTasksAndIdle);

        if (_viewModel.AppSettings.TrayEnabled)
        {
            _trayService = _serviceProvider.GetRequiredService<ITrayService>();
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

        DependencyPropertyDescriptor.FromProperty(TopmostProperty, typeof(Window))
            .AddValueChanged(this, OnTopmostChanged);
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = true;
        GlassBorder.Opacity = 1.0;
        ApplyIslandFocusPolicy(focused: true);
        SyncIslandTasksAndIdle();
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        _viewModel.IsWindowActive = false;
        GlassBorder.Opacity = 0.85;
        ApplyIslandFocusPolicy(focused: false);
        SyncIslandTasksAndIdle();
    }

    private void ApplyIslandFocusPolicy(bool focused)
    {
        if (_dynamicIslandWindow == null) return;
        _dynamicIslandWindow.ApplyFocusPolicy(focused, _viewModel.AppSettings.DynamicIslandWhenFocused);
        _dynamicIslandWindow.SetSessionMode(_viewModel.CurrentStatus);
    }

    private void SyncIslandTasksAndIdle()
    {
        if (!_viewModel.AppSettings.DynamicIslandEnabled) return;

        var island = GetDynamicIslandWindow();
        var chips = _viewModel.Tasks
            .Take(8)
            .Select(t => new DynamicIslandNotificationWindow.IslandTaskChip(
                t.Id, t.Name, string.IsNullOrWhiteSpace(t.Color) ? "#3B82F6" : t.Color));
        island.SetTasks(chips, _viewModel.SelectedTask?.Id);
        island.SetSessionMode(_viewModel.CurrentStatus);

        // Idle：失焦时展示待命岛（可点开选任务 + 开始）
        if (_viewModel.CurrentStatus == Models.TimerMode.Idle && !IsActive)
        {
            var mins = Math.Max(1, _viewModel.AppSettings.WorkMinutes);
            var label = _viewModel.SelectedTask?.Name ?? "选择任务";
            island.ShowIdleReady(label, $"{mins:D2}:00");
            ApplyIslandFocusPolicy(focused: false);
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnableAcrylicBackdrop();
        NavView.Navigate(typeof(TimerPage));
        ShowOnboardingIfNeeded();
        ShowDailyReportIfNeeded();
    }

    private void ShowOnboardingIfNeeded()
    {
        var settings = _viewModel.AppSettings;
        if (settings.HasCompletedOnboarding) return;

        Dispatcher.BeginInvoke(() =>
        {
            var win = new OnboardingWindow(settings) { Owner = this };
            win.ShowDialog();
            if (win.Completed)
            {
                _viewModel.StorageService.SaveSettings(settings);
                _viewModel.ReloadSettings();
            }
        });
    }

    private void ShowDailyReportIfNeeded()
    {
        var settings = _viewModel.AppSettings;
        if (!settings.HasCompletedOnboarding) return; // 首启先引导，日报下次再说
        if (!settings.DailyReportEnabled) return;
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
        // 置顶时不再强制藏岛；由 WhenFocused 策略统一处理
        ApplyIslandFocusPolicy(IsActive);
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
        catch (InvalidOperationException) { /* 鼠标未按下时 DragMove 无效 */ }
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
                return true;

            source = GetParentObject(source);
        }
        return false;
    }

    private static DependencyObject? GetParentObject(DependencyObject source)
    {
        if (source is Visual or Visual3D)
            return VisualTreeHelper.GetParent(source);
        if (source is FrameworkContentElement frameworkContentElement)
            return frameworkContentElement.Parent;
        if (source is ContentElement contentElement)
            return ContentOperations.GetParent(contentElement);
        return null;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowInAppNotification(string title, string message)
    {
        // 完成 / 预告 / 走神等走 Transient
        var island = GetDynamicIslandWindow();
        island.SetSessionMode(_viewModel.CurrentStatus);
        island.ShowNotification(title, message);
    }

    private void StartCountdown(string title)
    {
        var island = GetDynamicIslandWindow();
        var chips = _viewModel.Tasks
            .Take(8)
            .Select(t => new DynamicIslandNotificationWindow.IslandTaskChip(
                t.Id, t.Name, string.IsNullOrWhiteSpace(t.Color) ? "#3B82F6" : t.Color));
        island.SetTasks(chips, _viewModel.SelectedTask?.Id);
        island.SetSessionMode(_viewModel.CurrentStatus);
        island.StartCountdown(title);
        ApplyIslandFocusPolicy(IsActive);
    }

    private DynamicIslandNotificationWindow GetDynamicIslandWindow()
    {
        if (_dynamicIslandWindow != null) return _dynamicIslandWindow;

        _dynamicIslandWindow = new DynamicIslandNotificationWindow { Owner = this };
        WireIslandActions(_dynamicIslandWindow);
        return _dynamicIslandWindow;
    }

    private void WireIslandActions(DynamicIslandNotificationWindow island)
    {
        island.PauseRequested += () =>
        {
            if (_viewModel.PauseCommand.CanExecute(null))
                _viewModel.PauseCommand.Execute(null);
            island.SetSessionMode(_viewModel.CurrentStatus);
        };
        island.ResumeRequested += () =>
        {
            if (_viewModel.ResumeCommand.CanExecute(null))
                _viewModel.ResumeCommand.Execute(null);
            island.SetSessionMode(_viewModel.CurrentStatus);
        };
        island.SkipBreakRequested += () =>
        {
            if (_viewModel.SkipBreakCommand.CanExecute(null))
                _viewModel.SkipBreakCommand.Execute(null);
            island.SetSessionMode(_viewModel.CurrentStatus);
        };
        island.StartFocusRequested += () =>
        {
            if (_viewModel.StartFocusCommand.CanExecute(null))
                _viewModel.StartFocusCommand.Execute(null);
            island.SetSessionMode(_viewModel.CurrentStatus);
        };
        island.TaskSelected += taskId =>
        {
            _viewModel.SelectTaskById(taskId);
            SyncIslandTasksAndIdle();
        };
        island.OpenMainWindowRequested += () =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Show();
            Activate();
        };
    }

    private void UpdateCountdown(string remainingTime)
    {
        if (_dynamicIslandWindow == null) return;
        var chips = _viewModel.Tasks
            .Take(8)
            .Select(t => new DynamicIslandNotificationWindow.IslandTaskChip(
                t.Id, t.Name, string.IsNullOrWhiteSpace(t.Color) ? "#3B82F6" : t.Color));
        _dynamicIslandWindow.SetTasks(chips, _viewModel.SelectedTask?.Id);
        _dynamicIslandWindow.SetSessionMode(_viewModel.CurrentStatus);
        _dynamicIslandWindow.UpdateCountdown(remainingTime);
        ApplyIslandFocusPolicy(IsActive);
    }

    private void StopCountdown()
    {
        _dynamicIslandWindow?.HideCountdown();
    }

    private void ShowFullscreenBreak(string title, string remaining, bool allowEndEarly)
    {
        _fullscreenBreakWindow ??= new FullscreenBreakWindow();
        _fullscreenBreakWindow.EndBreakRequested -= OnFullscreenBreakEndRequested;
        _fullscreenBreakWindow.EndBreakRequested += OnFullscreenBreakEndRequested;
        _fullscreenBreakWindow.ShowBreak(title, remaining, allowEndEarly);
    }

    private void HideFullscreenBreak()
    {
        if (_fullscreenBreakWindow == null) return;
        _fullscreenBreakWindow.EndBreakRequested -= OnFullscreenBreakEndRequested;
        _fullscreenBreakWindow.ForceClose();
        _fullscreenBreakWindow = null;
    }

    private void OnFullscreenBreakEndRequested()
    {
        // 与主界面「结束休息」一致
        if (_viewModel.EndBreakCommand.CanExecute(null))
            _viewModel.EndBreakCommand.Execute(null);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var settings = _viewModel.AppSettings;

        // 关到托盘不算退出，不弹确认
        if (settings.TrayEnabled && settings.CloseToTray && _trayService != null)
        {
            e.Cancel = true;
            Hide();
            _trayService.ShowNotification("Lumen Pomodoro", "已最小化到托盘");
            return;
        }

        if (!_viewModel.ConfirmExitIfNeeded(this))
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DependencyPropertyDescriptor.FromProperty(TopmostProperty, typeof(Window))
            .RemoveValueChanged(this, OnTopmostChanged);

        _dynamicIslandWindow?.ForceClose();
        HideFullscreenBreak();
        _viewModel.Dispose();
        _trayService?.Dispose();
        _pageProvider.Dispose();
        base.OnClosed(e);
    }

    private sealed class PageProvider : INavigationViewPageProvider, IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private TimerPage? _timerPage;
        private TasksPage? _tasksPage;
        private StatsPage? _statsPage;
        private SettingsPage? _settingsPage;
        private SettingsViewModel? _settingsViewModel;

        public PageProvider(MainViewModel viewModel, IServiceProvider serviceProvider)
        {
            _viewModel = viewModel;
            _serviceProvider = serviceProvider;
        }

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
            var tasksVM = _serviceProvider.GetRequiredService<TasksViewModel>();
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
            var statsVM = _serviceProvider.GetRequiredService<StatsViewModel>();
            var exportSvc = _serviceProvider.GetRequiredService<IExportService>();
            var page = new StatsPage(statsVM, exportSvc);
            var mainWindow = (MainWindow?)Application.Current.MainWindow;
            page.RequestNavigateToTasks += () => mainWindow?.NavigateToPage(typeof(TasksPage));
            // 洞察→行动闭环（A1）：弱科目「现在专注」按钮 → 直接以该科目开始专注
            statsVM.StartFocusCallback = name => _viewModel.StartFocusWithTask(name);
            // 峰值时段排程（A2）：黄金时段「加入今日」按钮 → 写入今日计划
            statsVM.ScheduleBlockCallback = (name, hour) => _viewModel.AddToPlan(name, hour);
            statsVM.RemovePlanBlockCallback = blockId => _viewModel.RemovePlanBlock(blockId);
            return page;
        }

        private SettingsPage CreateSettingsPage()
        {
            _settingsViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
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
