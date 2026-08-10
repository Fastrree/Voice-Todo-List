using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class TodoDetailPage : ContentPage
{
    private readonly TodoDetailPageViewModel _viewModel;

    public TodoDetailPage(TodoDetailPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}