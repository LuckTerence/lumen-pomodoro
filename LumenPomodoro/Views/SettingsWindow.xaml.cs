using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LumenPomodoro.Services;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(StorageService storageService, CameraService cameraService)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(storageService, cameraService);
        DataContext = _viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !HasInteractiveParent(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private static bool HasInteractiveParent(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase || source is TextBox || source is ComboBox || source is ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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
