using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class FullscreenBreakWindow : Window
{
    private bool _allowClose;

    public event Action? EndBreakRequested;

    public FullscreenBreakWindow()
    {
        InitializeComponent();
    }

    public void ShowBreak(string title, string remainingTime, bool allowEndEarly)
    {
        TitleBlock.Text = title;
        SubtitleBlock.Text = Properties.LocalizedStrings.FullscreenBreak_Hint;
        CountdownBlock.Text = remainingTime;
        ApplyAllowEndEarly(allowEndEarly);

        if (!IsVisible)
        {
            WindowState = WindowState.Maximized;
            Show();
            Activate();
        }
    }

    public void UpdateCountdown(string remainingTime) => CountdownBlock.Text = remainingTime;

    public void ApplyAllowEndEarly(bool allow)
    {
        EndBreakButton.IsEnabled = allow;
        EndBreakButton.Visibility = allow ? Visibility.Visible : Visibility.Collapsed;
        StrictHintBlock.Visibility = allow ? Visibility.Collapsed : Visibility.Visible;
    }

    private void EndBreakButton_Click(object sender, RoutedEventArgs e)
    {
        if (EndBreakButton.IsEnabled)
            EndBreakRequested?.Invoke();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EndBreakButton.IsEnabled)
            EndBreakRequested?.Invoke();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _allowClose = true;
        try
        {
            Close();
        }
        catch
        {
            Hide();
        }
    }
}
