using System.Windows;
using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class FocusCompleteDialog : Window
{
    public bool ShouldStartBreak { get; private set; }

    public FocusCompleteDialog()
    {
        InitializeComponent();
        ShouldStartBreak = false;
        
        KeyDown += FocusCompleteDialog_KeyDown;
    }

    private void FocusCompleteDialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ShouldStartBreak = false;
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            ShouldStartBreak = true;
            DialogResult = true;
            Close();
        }
    }

    private void StartBreak_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartBreak = true;
        DialogResult = true;
        Close();
    }

    private void LaterBreak_Click(object sender, RoutedEventArgs e)
    {
        ShouldStartBreak = false;
        DialogResult = false;
        Close();
    }
}
