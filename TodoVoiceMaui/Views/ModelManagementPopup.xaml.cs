using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.ViewModels;

namespace TodoVoiceMaui.Views;

/// <summary>
/// Model Yönetimi modalı — çevrimdışı Whisper kataloğunu tek ekranda yönetir:
/// her model için detaylı bilgi (boyut/RAM/WER/dil/kuantizasyon/hız/öneri),
/// kurulu durumu + disk boyutu, indirme (yeşil bar + iptal) ve silme.
/// Aktif model silinemez; indirme arka planda sürerken popup açık kalır ve
/// ilerleme canlı güncellenir (SpeechToTextService PropertyChanged aboneliği).
/// </summary>
public partial class ModelManagementPopup : Popup
{
    private readonly ModelManagementViewModel _viewModel;

    public ModelManagementPopup(SpeechToTextService stt, SettingsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = new ModelManagementViewModel(stt, viewModel);
        BindingContext = _viewModel;
        _viewModel.CloseRequested += () => Close();
        // Konsol otomatik kaydırma
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        // Popup kapanınca aboneliği çöz — singleton servise sızıntı olmaz
        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        };
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModelManagementViewModel.TestConsoleFormatted) && PopupTestConsoleScroll != null)
        {
            try
            {
                await PopupTestConsoleScroll.ScrollToAsync(0, PopupTestConsoleScroll.ContentSize.Height, false);
            }
            catch { }
        }
    }
}

/// <summary>Popup başlığı: satır listesi + toplam disk + aktif model.</summary>
public partial class ModelManagementViewModel : ObservableObject, IDisposable
{
    private readonly SpeechToTextService _stt;
    private readonly SettingsPageViewModel _settings;

    public ObservableCollection<ModelManagementRow> Rows { get; } = new();

    [ObservableProperty]
    private string totalDiskText = string.Empty;

    [ObservableProperty]
    private string currentModelText = string.Empty;

    private readonly List<SttLogEntry> _consoleLines = new();

    private FormattedString _testConsoleFormatted = new();

    /// <summary>Renkli konsol içeriği (satır tiplerine göre Span renkleri).</summary>
    public FormattedString TestConsoleFormatted
    {
        get => _testConsoleFormatted;
        private set => SetProperty(ref _testConsoleFormatted, value);
    }

    public event Action? CloseRequested;

    public ModelManagementViewModel(SpeechToTextService stt, SettingsPageViewModel settings)
    {
        _stt = stt;
        _settings = settings;
        foreach (var model in WhisperModelCatalog.All)
            Rows.Add(new ModelManagementRow(stt, settings, model));

        _stt.PropertyChanged += OnSttPropertyChanged;
        _settings.ModelStateChanged += OnModelStateChanged;
        SttTestLog.Line += OnTestLogEntry;
        RefreshAll();
    }

    private void OnTestLogEntry(SttLogEntry entry)
    {
        const int maxLines = 200;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _consoleLines.Add(entry);
            if (_consoleLines.Count > maxLines)
                _consoleLines.RemoveAt(0);
            RebuildConsole();
        });
    }

    private void RebuildConsole()
    {
        var fs = new FormattedString();
        foreach (var entry in _consoleLines)
        {
            fs.Spans.Add(new Span
            {
                Text = entry.Text + Environment.NewLine,
                TextColor = SttConsolePalette.For(entry.Kind),
                FontFamily = "Consolas",
                FontSize = 10
            });
        }
        TestConsoleFormatted = fs;
    }

    [RelayCommand]
    private void ClearTestConsole()
    {
        _consoleLines.Clear();
        TestConsoleFormatted = new FormattedString();
    }

    private void OnSttPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpeechToTextService.IsDownloading)
            or nameof(SpeechToTextService.ModelDownloadProgress)
            or nameof(SpeechToTextService.ModelDownloadedBytes)
            or nameof(SpeechToTextService.ModelDownloadTotalBytes)
            or nameof(SpeechToTextService.IsModelReady))
        {
            RefreshAll();
        }
    }

    private void OnModelStateChanged(object? sender, EventArgs e) => RefreshAll();

    /// <summary>
    /// İndirme sürerken DISK STAT ÇAĞRILMAZ (her 80KB chunk'ta binlerce FileInfo
    /// sorgusu UI'ı dondurur) — yalnız ilerleme alanları güncellenir; kurulu durum
    /// ve toplam disk son hesaplanan değerde kalır, indirme bitince tazelenir.
    /// </summary>
    private void RefreshAll()
    {
        var downloading = _stt.IsDownloading;
        foreach (var row in Rows)
            row.Update(downloading);

        if (!downloading)
            TotalDiskText = $"Toplam disk: {FormatBytes(_stt.ModelDirectoryTotalBytes)} · {Rows.Count} model";
        CurrentModelText = $"Aktif model: {_stt.SelectedModel.DisplayName}";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public void Dispose()
    {
        _stt.PropertyChanged -= OnSttPropertyChanged;
        _settings.ModelStateChanged -= OnModelStateChanged;
        SttTestLog.Line -= OnTestLogEntry;
        GC.SuppressFinalize(this);
    }

    internal static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        return $"{bytes / 1024.0:0} KB";
    }
}

/// <summary>Katalogdaki tek bir modelin popup satırı — durum + komutlar.</summary>
public partial class ModelManagementRow : ObservableObject
{
    private readonly SpeechToTextService _stt;
    private readonly SettingsPageViewModel _settings;

    public WhisperModelInfo Model { get; }

    public string DisplayName => Model.DisplayName;
    public string TierLabel => Model.TierLabel;
    public string SizeLabel => Model.SizeLabel;
    public string SpeedLabel => Model.SpeedLabel;
    public string AccuracyLabel => Model.AccuracyLabel;
    public string RamLabel => Model.RamLabel;
    public string WerLabel => Model.WerLabel;
    public string LanguagesLabel => Model.LanguagesLabel;
    public string QuantizationLabel => Model.QuantizationLabel;
    public string SpeedFactorLabel => Model.SpeedFactorLabel;
    public string RecommendationText => Model.RecommendationText;

    [ObservableProperty]
    private bool isCurrent;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    private string installedText = "İndirilmemiş";

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private string downloadText = string.Empty;

    [ObservableProperty]
    private bool canDelete;

    [ObservableProperty]
    private bool canDownload;

    public ModelManagementRow(SpeechToTextService stt, SettingsPageViewModel settings, WhisperModelInfo model)
    {
        _stt = stt;
        _settings = settings;
        Model = model;
    }

    /// <summary>
    /// Satırı tazele. `downloading=true` iken disk stat YAPILMAZ (performans) —
    /// kurulu durum son hesaplanan değerde kalır; indirme bitince yeniden hesaplanır.
    /// </summary>
    public void Update(bool downloading)
    {
        IsCurrent = Model.Id == _stt.SelectedModel.Id;
        if (!downloading)
        {
            IsInstalled = _stt.IsModelInstalled(Model);
            InstalledText = IsInstalled
                ? $"Kurulu · {ModelManagementViewModel.FormatBytes(_stt.GetModelSizeOnDisk(Model))}"
                : "İndirilmemiş";
        }
        IsDownloading = downloading && IsCurrent;
        DownloadProgress = _stt.ModelDownloadProgress;
        DownloadText = IsDownloading
            ? $"İndiriliyor %{_stt.ModelDownloadProgress * 100:0} · " +
              $"{ModelManagementViewModel.FormatBytes(_stt.ModelDownloadedBytes)}/" +
              $"{ModelManagementViewModel.FormatBytes(_stt.ModelDownloadTotalBytes)}"
            : string.Empty;
        CanDelete = IsInstalled && !IsCurrent && !downloading;
        CanDownload = !IsInstalled && !downloading;
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (_stt.IsDownloading)
        {
            await Shell.Current.DisplayAlert("İndirme sürüyor",
                "Başka bir model indiriliyor. Bittiğinde tekrar deneyin.", "Tamam");
            return;
        }

        var ok = await _settings.EnsureSttModelAsync(Model, confirmLarge: true);
        if (ok)
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
        Update(_stt.IsDownloading);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_stt.IsDownloading)
            return;

        var ok = await Shell.Current.DisplayAlert("Modeli sil",
            $"{Model.DisplayName} ({Model.SizeLabel}) diskten silinsin mi?\n\n" +
            "Tekrar kullanmak için yeniden indirmen gerekecek.",
            "Sil", "Vazgeç");
        if (!ok)
            return;

        var success = _settings.DeleteSttModel(Model);
        if (success)
        {
            SoundEffectService.Play(SoundEffectService.SoundKind.Delete);
        }
        else
        {
            await Shell.Current.DisplayAlert("Silinemedi",
                "Aktif model silinemez veya dosya şu an kilitli. Önce başka bir modele geç.", "Tamam");
        }
        Update(_stt.IsDownloading);
    }

    [RelayCommand]
    private void CancelDownload() => _stt.CancelModelDownload();
}
