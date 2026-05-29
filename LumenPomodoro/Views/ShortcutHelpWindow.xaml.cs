using System.Windows.Input;

namespace LumenPomodoro.Views;

public partial class ShortcutHelpWindow : System.Windows.Window
{
    public ShortcutHelpWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape || e.Key == Key.Enter) Close(); };
    }
}
