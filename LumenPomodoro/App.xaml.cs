using System.IO;
using System.Windows;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui.Appearance;

namespace LumenPomodoro;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public static T GetRequiredService<T>() where T : notnull
        => ((App)Current).Services.GetRequiredService<T>();

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Bootstrap Serilog before anything else
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumenPomodoro");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "lumen-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("应用启动");

        // 2. Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 3. Wire global exception handlers (now using Serilog)
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        try
        {
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "启动失败");
            MessageBox.Show($"启动失败：{ex.Message}\n\n{ex.InnerException?.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Generate default sound files
        SoundService.GenerateDefaultWavFiles();

        ApplyThemeOnStartup();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services - interfaces → implementations
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<ISoundService, SoundService>();
        services.AddSingleton<IInsightEngine, InsightEngine>();
        services.AddSingleton<IExportService, ExportService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<StatsViewModel>();
        services.AddTransient<TasksViewModel>();

        // TrayService — registered as singleton, resolved manually in MainWindow
        services.AddSingleton<ITrayService, TrayService>();
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "未观察到的任务异常");
        e.SetObserved();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Dispatcher 未处理异常");

        try
        {
            MessageBox.Show($"发生未预期的错误：{e.Exception.Message}\n\n软件将继续运行，但部分功能可能受影响。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "AppDomain 未处理异常");

            try
            {
                MessageBox.Show($"发生严重错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Current.Dispatcher.BeginInvoke(() =>
            {
                if (Current.MainWindow is Views.MainWindow mainWindow)
                {
                    mainWindow.HandleWake();
                }
            });
        }
    }

    private void ApplyThemeOnStartup()
    {
        var storageService = Services.GetRequiredService<IStorageService>();
        var settings = storageService.LoadSettings();
        ApplyTheme(settings.Theme);
    }

    public void ApplyTheme(string theme)
    {
        switch (theme.ToLower())
        {
            case "dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case "light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            default:
                var systemTheme = ApplicationThemeManager.GetSystemTheme();
                ApplicationThemeManager.Apply(
                    systemTheme == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                break;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        Log.Information("应用退出");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
