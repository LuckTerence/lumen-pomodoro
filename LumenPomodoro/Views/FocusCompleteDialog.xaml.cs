using System.Windows;
using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class FocusCompleteDialog : Window
{
    public bool ShouldStartBreak { get; private set; }
    public bool ShouldStartLongBreak { get; private set; }

    public FocusCompleteDialog()
    {
        InitializeComponent();
        ShouldStartBreak = false;
        ShouldStartLongBreak = false;

        KeyDown += FocusCompleteDialog_KeyDown;
    }

    public void SetLongBreakSuggestion(bool suggest)
    {
        if (!IsInitialized) return;

        LongBreakButton.Visibility = suggest ? Visibility.Visible : Visibility.Collapsed;
        if (suggest)
        {
            SuggestionText.Text = $"已连续完成 {SuggestCount} 个番茄钟，建议进行长休息！";
            SuggestionText.Visibility = Visibility.Visible;
        }
    }

    public int SuggestCount { get; set; }

    private void FocusCompleteDialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ShouldStartBreak = false;
            ShouldStartLongBreak = false;
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            ShouldStartBreak = true;
            ShouldStartLongBreak = LongBreakButton.Visibility == Visibility.Visible;
            DialogResult = true;
            Close();
        }
    }

    private void StartBreak_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartBreak = true;
        ShouldStartLongBreak = false;
        DialogResult = true;
        Close();
    }

    private void StartLongBreak_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartBreak = true;
        ShouldStartLongBreak = true;
        DialogResult = true;
        Close();
    }

    private void LaterBreak_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartBreak = false;
        ShouldStartLongBreak = false;
        DialogResult = false;
        Close();
    }
}
