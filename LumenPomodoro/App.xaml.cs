using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services;
using Microsoft.Win32;

namespace LumenPomodoro;

public partial class App : Application
{
    public StorageService StorageService { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        StorageService = new StorageService();

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        base.OnStartup(e);

        SoundService.GenerateDefaultWavFiles();

        ApplyThemeOnStartup();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);

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
            LogException(ex);

            try
            {
                MessageBox.Show($"发生严重错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }

    private static void LogException(Exception ex)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LumenPomodoro");
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
            Debug.WriteLine(ex);
        }
        catch
        {
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
        var settings = StorageService.LoadSettings();
        ApplyTheme(settings.Theme);
    }

    public void ApplyTheme(string theme)
    {
        var resources = Resources.MergedDictionaries;

        var themeDictionaries = resources
            .Where(r => r.Source?.ToString().Contains("Themes/") == true)
            .ToList();

        var newTheme = theme.ToLower() switch
        {
            "dark" => new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) },
            "light" => new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) },
            _ => IsSystemDarkMode()
                ? new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) }
                : new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) }
        };

        // 先添加新主题，再移除旧主题，避免 DynamicResource 查找空窗
        resources.Add(newTheme);
        foreach (var dict in themeDictionaries)
        {
            resources.Remove(dict);
        }
    }

    private bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value != null && (int)value == 0;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        base.OnExit(e);
    }
}
