using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumenPomodoro.Services;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Microsoft.Win32;

namespace LumenPomodoro.Views.Pages;

public partial class StatsPage : Page
{
    private readonly StatsViewModel _viewModel;
    public event Action? RequestNavigateToTasks;

    public StatsPage(StatsViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Refresh();
    }

    public void Refresh()
    {
        _viewModel.Refresh();
    }

    private void PrevDate_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShiftDate(-1);
    }

    private void NextDate_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShiftDate(1);
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext == null) return;
        if (PeriodCombo.SelectedIndex < 0) return;
        var value = PeriodCombo.SelectedIndex switch
        {
            0 => "Day",
            1 => "Week",
            2 => "Month",
            _ => "Day"
        };
        _viewModel.PeriodSelection = value;
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"lumen_pomodoro_{DateTime.Today:yyyy-MM-dd}"
        };
        if (dialog.ShowDialog() == true)
        {
            var exportService = new ExportService();
            exportService.ExportToFile(_viewModel.GetAllSessions(), dialog.FileName, ExportFormat.Csv);
        }
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json",
            DefaultExt = ".json",
            FileName = $"lumen_pomodoro_{DateTime.Today:yyyy-MM-dd}"
        };
        if (dialog.ShowDialog() == true)
        {
            var exportService = new ExportService();
            exportService.ExportToFile(_viewModel.GetAllSessions(), dialog.FileName, ExportFormat.Json);
        }
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void AddMissingCategory_Click(object sender, RoutedEventArgs e)
    {
        RequestNavigateToTasks?.Invoke();
    }
}
