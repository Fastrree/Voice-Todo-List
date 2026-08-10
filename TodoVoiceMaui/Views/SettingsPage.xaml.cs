using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageViewModel _viewModel;

    public SettingsPage(SettingsPageViewModel viewModel)
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

    private void OnThemePickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.ThemeOptions[picker.SelectedIndex];
            _viewModel.SelectedTheme = item.Key;
        }
    }

    private void OnLanguagePickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.LanguageOptions[picker.SelectedIndex];
            _viewModel.SelectedLanguage = item.Key;
        }
    }
}
