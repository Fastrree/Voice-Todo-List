using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class TodoListPage : ContentPage
{
    private readonly TodoListPageViewModel _viewModel;

    public TodoListPage(TodoListPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private void OnPriorityPickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.PriorityFilterOptions[picker.SelectedIndex];
            _viewModel.ChangePriorityCommand.Execute(item.Key);
        }
    }

    private void OnSortPickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.SortOptions[picker.SelectedIndex];
            _viewModel.ChangeSortCommand.Execute(item.Key);
        }
    }
}