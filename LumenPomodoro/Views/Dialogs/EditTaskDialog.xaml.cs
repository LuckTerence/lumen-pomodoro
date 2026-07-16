using System.Windows;
using Wpf.Ui.Controls;

namespace LumenPomodoro.Views.Dialogs;

public partial class EditTaskDialog : FluentWindow
{
    public string TaskName => TaskNameBox.Text;

    public EditTaskDialog(string currentName)
    {
        InitializeComponent();
        TaskNameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            TaskNameBox.Focus();
            TaskNameBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
