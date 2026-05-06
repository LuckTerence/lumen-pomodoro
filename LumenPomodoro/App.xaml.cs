using System.Configuration;
using System.Data;
using System.Windows;
using LumenPomodoro.Models;
using LumenPomodoro.Services;

namespace LumenPomodoro;

public partial class App : Application
{
    private readonly StorageService _storageService = new StorageService();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyThemeOnStartup();
    }

    private void ApplyThemeOnStartup()
    {
        var settings = _storageService.LoadSettings();
        ApplyTheme(settings.Theme);
    }

    public void ApplyTheme(string theme)
    {
        var resources = Resources.MergedDictionaries;

        var themeDictionaries = resources
            .Where(r => r.Source?.ToString().Contains("Themes/") == true)
            .ToList();
        
        foreach (var dict in themeDictionaries)
        {
            resources.Remove(dict);
        }

        ResourceDictionary themeDictionary;
        switch (theme.ToLower())
        {
            case "dark":
                themeDictionary = new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) };
                break;
            case "light":
                themeDictionary = new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) };
                break;
            case "system":
            default:
                var isDark = IsSystemDarkMode();
                themeDictionary = isDark 
                    ? new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) }
                    : new ResourceDictionary { Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative) };
                break;
        }

        resources.Add(themeDictionary);
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
}

