using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;
using LumenPomodoro.Views.Pages;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace LumenPomodoro.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private TrayService? _trayService;
    private readonly PageProvider _pageProvider;

    public MainWindow()
    {
        InitializeComponent();

        var storageService = ((App)Application.Current).StorageService;
        _viewModel = new MainViewModel(storageService);
        _pageProvider = new PageProvider(_viewModel);
        DataContext = _viewModel;

        if (_viewModel.AppSettings.TrayEnabled)
        {
            _trayService = new TrayService(_viewModel, _viewModel.CameraService, _viewModel.StorageService);
            _trayService.AttachToWindow(this);

            _viewModel.TrayMenuNeedsUpdate += () =>
                Dispatcher.BeginInvoke(() => _trayService.UpdateMenuState());

            _viewModel.NotificationRequested += (title, message) =>
                Dispatcher.BeginInvoke(() => _trayService.ShowNotification(title, message));
        }

        NavView.SetPageProviderService(_pageProvider);

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(TimerPage));
    }

    public void NavigateToPage(Type pageType)
    {
        NavView.Navigate(pageType);
    }

    public void HandleWake()
    {
        _viewModel.RefreshTimerOnWake();
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
        _viewModel.Dispose();
        _trayService?.Dispose();
        base.OnClosed(e);
    }

    private sealed class PageProvider : INavigationViewPageProvider
    {
        private readonly MainViewModel _viewModel;
        private TimerPage? _timerPage;
        private TasksPage? _tasksPage;
        private StatsPage? _statsPage;
        private SettingsPage? _settingsPage;

        public PageProvider(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? GetPage(Type pageType)
        {
            if (pageType == typeof(TimerPage))
                return _timerPage ??= CreateTimerPage();
            if (pageType == typeof(TasksPage))
                return _tasksPage ??= new TasksPage(new TasksViewModel(_viewModel.StorageService));
            if (pageType == typeof(StatsPage))
                return _statsPage ??= new StatsPage(new StatsViewModel(_viewModel.StorageService));
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

        private SettingsPage CreateSettingsPage()
        {
            var settingsVM = new SettingsViewModel(_viewModel.StorageService, _viewModel.CameraService);
            var page = new SettingsPage(settingsVM);
            page.SettingsSaved += () =>
            {
                _viewModel.ReloadSettings();
                _viewModel.RefreshStats();
            };
            return page;
        }
    }
}
