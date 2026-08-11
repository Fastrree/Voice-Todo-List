using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;
namespace TodoVoiceMaui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageViewModel _viewModel;
    private bool _isSyncingPickers;
    private bool _hasAnimated;

    public SettingsPage(SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Canlı konsola yeni satır eklendiğinde en alta kaydır (terminal davranışı).</summary>
    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsPageViewModel.TestConsoleFormatted) && TestConsoleScroll != null)
        {
            try
            {
                await TestConsoleScroll.ScrollToAsync(0, TestConsoleScroll.ContentSize.Height, false);
            }
            catch { }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
        SyncPickerSelections();
        if (!_hasAnimated)
        {
            _hasAnimated = true;
            if (SettingsScroll != null)
                await AnimationService.FadeSlideInAsync(SettingsScroll, 0, 380, 18);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Singleton servislere abone olan transient ViewModel — sızıntıyı önle
        _viewModel.Dispose();
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

            var sttIndex = _viewModel.SttModels.ToList().FindIndex(m => m.Id == _viewModel.SelectedSttModel?.Id);
            if (sttIndex >= 0) SttModelPicker.SelectedIndex = sttIndex;

            var providerIndex = _viewModel.SpeechProviders.ToList().FindIndex(p => p.Id == _viewModel.SelectedSpeechProvider?.Id);
            if (providerIndex >= 0) SpeechProviderPicker.SelectedIndex = providerIndex;
        }
        finally
        {
            _isSyncingPickers = false;
        }
    }

    private void OnSttModelPickerChanged(object? sender, EventArgs e)
    {
        if (_isSyncingPickers) return;

        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            _viewModel.SelectedSttModel = _viewModel.SttModels[picker.SelectedIndex];
        }
    }

    private void OnSpeechProviderPickerChanged(object? sender, EventArgs e)
    {
        if (_isSyncingPickers) return;

        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            _viewModel.SelectedSpeechProvider = _viewModel.SpeechProviders[picker.SelectedIndex];
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

    /// <summary>
    /// Windows Hello kilidi Switch'i — doğrudan bağlama YOK (VM doğrulama yapıp
    /// kendisi ayarlar). VM geri yazınca Toggled tekrar tetiklenir; VM içindeki
    /// eşitlik guard'ı döngüyü kırar.
    /// </summary>
    private async void OnBiometricLockToggled(object? sender, ToggledEventArgs e)
    {
        await _viewModel.SetBiometricLockAsync(e.Value);
    }
}
