using System.Windows;
using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class SettingsPage : Page, IDisposable
{
    private readonly SettingsViewModel _viewModel;

    public event Action? SettingsSaved;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Unloaded += SettingsPage_Unloaded;
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Cleanup();
    }

    private void TestCamera_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TestCameraAlert();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        SettingsSaved?.Invoke();
    }

    public void Dispose()
    {
        Unloaded -= SettingsPage_Unloaded;
        _viewModel.Dispose();
    }
}
