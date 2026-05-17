using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        SettingsSaved?.Invoke();
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    public void Dispose()
    {
        Unloaded -= SettingsPage_Unloaded;
        _viewModel.Dispose();
    }
}
