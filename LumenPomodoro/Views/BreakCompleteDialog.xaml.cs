using System.Windows;
using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class BreakCompleteDialog : Window
{
    public bool ShouldStartNext { get; private set; } = false;

    public BreakCompleteDialog()
    {
        InitializeComponent();
        
        KeyDown += BreakCompleteDialog_KeyDown;
    }

    private void BreakCompleteDialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            ShouldStartNext = true;
            DialogResult = true;
            Close();
        }
    }

    private void StartNext_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartNext = true;
        DialogResult = true;
        Close();
    }
}
