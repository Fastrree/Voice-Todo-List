using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageViewModel _viewModel;
    private bool _isSyncingPickers;

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
        SyncPickerSelections();
    }

    private void SyncPickerSelections()
    {
        // Profilden yüklenen değerleri Picker'lara yansıt (kayıtlı tema doğru görünsün)
        _isSyncingPickers = true;
        try
        {
            var langIndex = _viewModel.LanguageOptions.FindIndex(o => o.Key == _viewModel.SelectedLanguage);
            if (langIndex >= 0) LanguagePicker.SelectedIndex = langIndex;

            var themeIndex = _viewModel.ThemeOptions.FindIndex(o => o.Key == _viewModel.SelectedTheme);
            if (themeIndex >= 0) ThemePicker.SelectedIndex = themeIndex;
        }
        finally
        {
            _isSyncingPickers = false;
        }
    }

    private void OnThemePickerChanged(object? sender, EventArgs e)
    {
        // SyncPickerSelections programatik set sırasında tetiklenir — atla
        if (_isSyncingPickers) return;

        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var item = _viewModel.ThemeOptions[picker.SelectedIndex];
            _viewModel.SelectedTheme = item.Key;

            // Tema anında uygulanır (önizleme) + tercih kaydedilir
            ThemeService.ApplyTheme(item.Key);
            ThemeService.SaveTheme(item.Key);
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
