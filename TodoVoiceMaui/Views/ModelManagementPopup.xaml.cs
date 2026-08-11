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

    /// <summary>Kaç model şu an indiriliyor? (Çoklu indirme göstergesi — başlıkta.)</summary>
    [ObservableProperty]
    private string activeDownloadsText = string.Empty;

    [ObservableProperty]
    private bool hasActiveDownloads;

    /// <summary>Üstteki "AKTİF İNDİRMELER" özet şeridi — her iş kendi mini çubuğu + iptaliyle.</summary>
    public ObservableCollection<ActiveDownloadRow> ActiveDownloadRows { get; } = new();

    private readonly Dictionary<string, ActiveDownloadRow> _activeRowCache = new();

    private readonly List<SttLogEntry> _consoleLines = new();

    private FormattedString _testConsoleFormatted = new();

    /// <summary>Renkli konsol içeriği (satır tiplerine göre Span renkleri).</summary>
    public FormattedString TestConsoleFormatted
    {
        get => _testConsoleFormatted;
        private set => SetProperty(ref _testConsoleFormatted, value);
    }

    private const string ConsoleFilterPreferenceKey = "stt_console_filter";

    /// <summary>Aktif konsol filtresi — yalnız render'ı etkiler, satırlar toplanmaya devam eder.</summary>
    [ObservableProperty]
    private SttConsoleFilter consoleFilter;

    /// <summary>
    /// Filtre değişince kalıcı kaydet (Settings ile aynı anahtar — tutarlı) + yeniden çiz.
    /// Popup açıkken Settings sayfasının hafızası da eşitlenir (arka planda eski filtre kalmasın).
    /// </summary>
    partial void OnConsoleFilterChanged(SttConsoleFilter value)
    {
        Preferences.Default.Set(ConsoleFilterPreferenceKey, (int)value);
        if (_settings.ConsoleFilter != value)
            _settings.ConsoleFilter = value;
        RebuildConsole();
    }

    [RelayCommand]
    private void SetConsoleFilter(SttConsoleFilter filter) => ConsoleFilter = filter;

    public event Action? CloseRequested;

    public ModelManagementViewModel(SpeechToTextService stt, SettingsPageViewModel settings)
    {
        _stt = stt;
        _settings = settings;
        foreach (var model in WhisperModelCatalog.All)
            Rows.Add(new ModelManagementRow(stt, settings, model));

        _stt.PropertyChanged += OnSttPropertyChanged;
        _stt.DownloadStateChanged += OnDownloadStateChanged;
        _settings.ModelStateChanged += OnModelStateChanged;
        SttTestLog.Line += OnTestLogEntry;
        ConsoleFilter = (SttConsoleFilter)Preferences.Default.Get(ConsoleFilterPreferenceKey, (int)SttConsoleFilter.All);
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
        foreach (var entry in FilterLines(_consoleLines))
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

    /// <summary>Aktif filtreye göre satırları seçer (All = hepsi).</summary>
    private IEnumerable<SttLogEntry> FilterLines(IEnumerable<SttLogEntry> lines) => ConsoleFilter switch
    {
        SttConsoleFilter.Success => lines.Where(l => l.Kind == SttLogKind.Success),
        SttConsoleFilter.Errors => lines.Where(l => l.Kind == SttLogKind.Error),
        SttConsoleFilter.Warnings => lines.Where(l => l.Kind == SttLogKind.Warning),
        _ => lines
    };

    [RelayCommand]
    private void ClearTestConsole()
    {
        _consoleLines.Clear();
        RebuildConsole();
    }

    /// <summary>Konsol satırlarını LOG dosyasına dışa aktarır (kind etiketli, tüm satırlar).</summary>
    [RelayCommand]
    private async Task ExportTestConsoleAsync()
    {
        if (_consoleLines.Count == 0)
        {
            await Shell.Current.DisplayAlert("Konsol boş", "Dışa aktarılacak satır yok.", "Tamam");
            return;
        }

        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var entry in _consoleLines)
                sb.AppendLine($"[{entry.Kind.ToString().ToUpperInvariant()}] {entry.Text}");

            // Milisaniye damgası: aynı saniyedeki iki dışa aktarma çakışmasın
            var path = Path.Combine(FileSystem.AppDataDirectory,
                $"TodoVoice_console_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
            await File.WriteAllTextAsync(path, sb.ToString());

            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            await Shell.Current.DisplayAlert("Dışa aktarıldı",
                $"{_consoleLines.Count} satır kaydedildi:\n{path}", "Tamam");
            TryOpenInExplorer(path);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Dışa aktarılamadı: {ex.Message}", "Tamam");
        }
    }

    private static void TryOpenInExplorer(string filePath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void OnSttPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpeechToTextService.IsDownloading)
            or nameof(SpeechToTextService.ModelDownloadProgress)
            or nameof(SpeechToTextService.ModelDownloadedBytes)
            or nameof(SpeechToTextService.ModelDownloadTotalBytes)
            or nameof(SpeechToTextService.IsModelReady))
        {
            RefreshOnUiThread();
        }
    }

    private void OnModelStateChanged(object? sender, EventArgs e) => RefreshOnUiThread();

    /// <summary>Herhangi bir indirme işi ilerleyince satırları tazele (her model kendi işine bağlı).</summary>
    private void OnDownloadStateChanged(object? sender, EventArgs e) => RefreshOnUiThread();

    /// <summary>
    /// Satırları UI thread'inde tazele. Normalde çağrılar zaten UI bağlamından gelir
    /// (await continuations) — o durumda doğrudan çağrı, kuyruk şişmesi olmaz.
    /// Gelecekte arka plandan gelen bir çağrı olursa (fire-and-forget iş) marshal edilir.
    /// </summary>
    private void RefreshOnUiThread()
    {
        if (MainThread.IsMainThread)
            RefreshAll();
        else
            MainThread.BeginInvokeOnMainThread(RefreshAll);
    }

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

        // Aktif indirmeler özet şeridi: satırları CANLI tut (koleksiyonu her chunk'ta
        // yeniden kurma — CollectionChanged bildirim yağmuru olmasın; yalnız başlayan
        // eklenir, biten çıkar, süren güncellenir).
        var activeJobs = _stt.Downloads.Where(j => j.IsActive).ToList();
        var activeIds = new HashSet<string>(activeJobs.Select(j => j.Model.Id));
        foreach (var staleId in _activeRowCache.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            if (_activeRowCache.Remove(staleId, out var staleRow))
                ActiveDownloadRows.Remove(staleRow);
        }
        foreach (var job in activeJobs)
        {
            if (!_activeRowCache.TryGetValue(job.Model.Id, out var activeRow))
            {
                activeRow = new ActiveDownloadRow(_stt, job.Model);
                _activeRowCache[job.Model.Id] = activeRow;
                ActiveDownloadRows.Add(activeRow);
            }
            activeRow.Update(job);
        }

        // Çoklu indirme göstergesi: kaç model aynı anda iniyor?
        var active = activeJobs.Count;
        HasActiveDownloads = active > 0;
        ActiveDownloadsText = active > 1
            ? $"{active} model aynı anda indiriliyor — her biri kendi çubuğunda"
            : active == 1
                ? "1 model indiriliyor — ilerleme kendi satırında"
                : string.Empty;
    }

    /// <summary>
    /// Kullanılmayan kurulu modelleri tek tıkla siler. Korunanlar: aktif model +
    /// en küçük kurulu model (çevrimdışı kullanıcı hafif bir çalışan modeli kaybetmesin).
    /// </summary>
    [RelayCommand]
    private async Task DeleteUnusedModelsAsync()
    {
        var unused = _stt.GetUnusedModels();
        if (unused.Count == 0)
        {
            await Shell.Current.DisplayAlert("Temizlenecek yok",
                "Kullanılmayan kurulu model bulunmuyor. Aktif model ve en küçük kurulu model her zaman korunur.",
                "Tamam");
            return;
        }

        var names = string.Join(", ", unused.Select(m => $"{m.DisplayName} ({m.SizeLabel})"));
        var totalMb = unused.Sum(m => m.SizeMb);
        var ok = await Shell.Current.DisplayAlert("Kullanılmayan modelleri sil",
            $"{names}\n\nToplam ~{totalMb:N0} MB disk boşalır.\n\n" +
            "Aktif model ve en küçük kurulu model KORUNUR. Devam edilsin mi?",
            "Sil", "Vazgeç");
        if (!ok)
            return;

        var deleted = 0;
        foreach (var m in unused)
        {
            if (_stt.DeleteModel(m))
                deleted++;
        }
        SoundEffectService.Play(deleted > 0
            ? SoundEffectService.SoundKind.Delete
            : SoundEffectService.SoundKind.Error);
        _settings.NotifyModelStateChanged();
        RefreshAll();
        if (deleted > 0)
            await Shell.Current.DisplayAlert("Tamamlandı", $"{deleted} model silindi.", "Tamam");
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public void Dispose()
    {
        _stt.PropertyChanged -= OnSttPropertyChanged;
        _stt.DownloadStateChanged -= OnDownloadStateChanged;
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

/// <summary>"AKTİF İNDİRMELER" şeridindeki tek indirme satırı — mini çubuk + iptal.</summary>
public partial class ActiveDownloadRow : ObservableObject
{
    private readonly SpeechToTextService _stt;

    public WhisperModelInfo Model { get; }

    public string DisplayName => Model.DisplayName;
    public string SizeLabel => Model.SizeLabel;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string progressText = string.Empty;

    public ActiveDownloadRow(SpeechToTextService stt, WhisperModelInfo model)
    {
        _stt = stt;
        Model = model;
    }

    /// <summary>İşten gelen değerleri satıra yazar (yalnız değişen PropertyChanged tetiklenir).</summary>
    public void Update(ModelDownloadJob job)
    {
        Progress = job.Progress;
        ProgressText = $"İndiriliyor %{job.Progress * 100:0} · " +
                       $"{ModelManagementViewModel.FormatBytes(job.DownloadedBytes)}/" +
                       $"{ModelManagementViewModel.FormatBytes(job.TotalBytes)}";
    }

    [RelayCommand]
    private void Cancel() => _stt.CancelModelDownload(Model);
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
    public void Update(bool anyDownloadActive)
    {
        IsCurrent = Model.Id == _stt.SelectedModel.Id;
        // Satır KENDİ modelinin işine bağlanır (çoklu indirme) — diğer satırların
        // indirmesi bu satırı etkilemez, her biri kendi çubuğunu gösterir.
        var job = _stt.GetDownloadJob(Model);
        var active = job?.IsActive == true;

        if (!anyDownloadActive)
        {
            IsInstalled = _stt.IsModelInstalled(Model);
            InstalledText = IsInstalled
                ? $"Kurulu · {ModelManagementViewModel.FormatBytes(_stt.GetModelSizeOnDisk(Model))}"
                : "İndirilmemiş";
        }
        IsDownloading = active;
        DownloadProgress = job?.Progress ?? 0;
        DownloadText = active && job != null
            ? $"İndiriliyor %{job.Progress * 100:0} · " +
              $"{ModelManagementViewModel.FormatBytes(job.DownloadedBytes)}/" +
              $"{ModelManagementViewModel.FormatBytes(job.TotalBytes)}"
            : string.Empty;
        CanDelete = IsInstalled && !IsCurrent && !active;
        CanDownload = !IsInstalled && !active;
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        // Çoklu indirme: seçimi değiştirmez, bu modeli arka planda indirir.
        // Başka model indirilirken de çalışır — her satır kendi işini başlatır.
        var ok = await _settings.DownloadSttModelAsync(Model, confirmLarge: true);
        if (ok)
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
        Update(_stt.IsDownloading);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        // Yalnız BU modelin indirmesi sürüyorsa silme engellenir (çoklu indirme)
        if (_stt.IsModelDownloading(Model))
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
    private void CancelDownload() => _stt.CancelModelDownload(Model);
}
