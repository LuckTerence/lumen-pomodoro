using System.Windows;
using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class BreakCompleteDialog : Window
{
    public BreakCompleteDialog()
    {
        InitializeComponent();
        
        KeyDown += BreakCompleteDialog_KeyDown;
    }

    private void BreakCompleteDialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Enter)
        {
            Close();
        }
    }

    private void StartNext_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
