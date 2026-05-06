using System.Windows;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        Close();
    }

    private void TestCameraButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.TestCameraAlert();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Cleanup();
    }
}