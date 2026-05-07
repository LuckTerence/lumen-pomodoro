using System.Windows.Controls;
using LumenPomodoro.ViewModels;

namespace LumenPomodoro.Views.Pages;

public partial class StatsPage : Page
{
    private readonly StatsViewModel _viewModel;

    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void Refresh()
    {
        _viewModel.Refresh();
    }
}
