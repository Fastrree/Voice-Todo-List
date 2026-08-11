using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.Views;

namespace TodoVoiceMaui.ViewModels;

public partial class SettingsPageViewModel : ObservableObject, IDisposable
{
    private readonly SyncService _syncService;
    private readonly ITodoStore _todoStore;
    private readonly SpeechToTextService _stt;

    [ObservableProperty]
    private UserProfile? userProfile;

    [ObservableProperty]
    private UserStats? userStats;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string selectedLanguage = "tr";

    [ObservableProperty]
    private string selectedTheme = "light";

    [ObservableProperty]
    private bool enableNotifications = true;

    [ObservableProperty]
    private bool enableVoiceRecording = true;

    [ObservableProperty]
    private bool autoSync = true;

    [ObservableProperty]
    private bool enableSoundEffects = true;

    [ObservableProperty]
    private string defaultPriority = "medium";

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isSyncing = false;

    [ObservableProperty]
    private DateTime lastSyncTime;

    [ObservableProperty]
    private bool isOnline = true;

    // ---- Ses Tanıma (STT) bölümü ----

    [ObservableProperty]
    private WhisperModelInfo? selectedSttModel;

    [ObservableProperty]
    private bool isSttDownloading;

    [ObservableProperty]
    private double sttDownloadProgress;

    [ObservableProperty]
    private string sttStatusText = string.Empty;

    [ObservableProperty]
    private string sttInstalledInfo = string.Empty;

    // ---- İndirme detayları (yeşil çubuk + modal) ----

    [ObservableProperty]
    private int sttDownloadPercent;

    [ObservableProperty]
    private string sttDownloadAmountText = string.Empty;

    [ObservableProperty]
    private string sttDownloadSpeedText = string.Empty;

    [ObservableProperty]
    private string sttDownloadInlineText = string.Empty;

    [ObservableProperty]
    private string sttDownloadFileLabel = string.Empty;

    // ---- Transkripsiyon kaynağı (çevrimdışı / bulut) ----

    [ObservableProperty]
    private SpeechProviderInfo? selectedSpeechProvider;

    [ObservableProperty]
    private string providerApiKey = string.Empty;

    [ObservableProperty]
    private string providerStatusText = string.Empty;

    [ObservableProperty]
    private bool isProviderTesting;

    // ---- Bölge (Azure vb.) ----

    [ObservableProperty]
    private string providerRegion = string.Empty;

    // ---- Biyometrik kilit (Windows Hello) ----

    [ObservableProperty]
    private bool isBiometricLockEnabled;

    [ObservableProperty]
    private bool isBiometricAvailable;

    [ObservableProperty]
    private bool isKeyLocked;

    [ObservableProperty]
    private string biometricStatusText = string.Empty;

    private bool _keyRevealed = true;

    /// <summary>Ayarlar sayfası kilitli mi? (Biyometrik kilit açıkken girişte overlay gösterilir.)</summary>
    [ObservableProperty]
    private bool isSettingsLocked;

    // ---- Canlı konsol + API anahtarı gizle/göster ----

    private readonly List<SttLogEntry> _consoleLines = new();

    private FormattedString _testConsoleFormatted = new();

    /// <summary>Renkli konsol içeriği (satır tiplerine göre Span renkleri).</summary>
    public FormattedString TestConsoleFormatted
    {
        get => _testConsoleFormatted;
        private set => SetProperty(ref _testConsoleFormatted, value);
    }

    [ObservableProperty]
    private bool isApiKeyMasked = true;

    public IReadOnlyList<SpeechProviderInfo> SpeechProviders { get; } =
        SpeechProviderCatalog.All.Where(p => p.Id == "offline" || p.IsImplemented).ToList();

    public IReadOnlyList<WhisperModelInfo> SttModels { get; } = WhisperModelCatalog.All;

    /// <summary>Model seçici yalnızca çevrimdışı kaynak seçiliyken gösterilir.</summary>
    public bool IsOfflineProvider => SelectedSpeechProvider?.Id == "offline";

    /// <summary>API anahtarı alanı yalnızca bulut sağlayıcıda gösterilir.</summary>
    public bool IsApiKeyVisible => SelectedSpeechProvider is { RequiresApiKey: true };

    /// <summary>Bölge alanı yalnızca bölge gerektiren sağlayıcıda gösterilir (Azure).</summary>
    public bool IsRegionRequired => SelectedSpeechProvider is { RequiresRegion: true };

    /// <summary>Seçili sağlayıcının katalog açıklaması (detay kartı).</summary>
    public string ProviderDetailText =>
        SelectedSpeechProvider == null ? string.Empty :
        $"{SelectedSpeechProvider.ModelLabel} · {SelectedSpeechProvider.CostLabel}\n{SelectedSpeechProvider.Description}";

    public SettingsPageViewModel(SyncService syncService, ITodoStore todoStore, SpeechToTextService stt)
    {
        _syncService = syncService;
        _todoStore = todoStore;
        _stt = stt;

        // Subscribe to sync service
        _syncService.PropertyChanged += OnSyncServicePropertyChanged;

        // STT model durumunu canlı tut (indirme ilerlemesi arayüze yansır)
        _stt.PropertyChanged += OnSttPropertyChanged;
        IsBiometricLockEnabled = Preferences.Default.Get("stt_biometric_lock", false);
        // Kilit açıksa İLK KARE'den itibaren overlay görünsün (doğrulama öncesi boşluk yok)
        IsSettingsLocked = IsBiometricLockEnabled;
        SttTestLog.Line += OnTestLogEntry;
        SelectedSttModel = _stt.SelectedModel;
        SelectedSpeechProvider = _stt.SelectedProvider;
        OnPropertyChanged(nameof(IsOfflineProvider));
        OnPropertyChanged(nameof(IsApiKeyVisible));
        OnPropertyChanged(nameof(IsRegionRequired));
    }

    /// <summary>
    /// Canlı konsol: test/indirme satırlarını biriktir. Transkriberlar Task.Run
    /// içinden log atabildiği için UI thread'ine marshal edilir (güvenli binding).
    /// Satır sayısı sınırlıdır (200) — eski satırlar düşer, bellek sonsuz büyümez.
    /// </summary>
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

    /// <summary>Satır tiplerine göre renklendirilmiş FormattedString kurar.</summary>
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
                FontSize = 11
            });
        }
        TestConsoleFormatted = fs;
    }

    public async Task InitializeAsync()
    {
        await LoadUserProfileAsync();
        await LoadUserStatsAsync();
        LoadSyncInfo();
        RefreshSttStatus();
        await RefreshBiometricStateAsync();
        await HandleEntryLockAsync();
    }

    /// <summary>
    /// Biyometrik kilit açıksa Ayarlar'a girişte Windows Hello ister.
    /// Kullanıcı onaylarsa sayfa açılır; iptal ederse kilit overlay'i kalır.
    /// </summary>
    private async Task HandleEntryLockAsync()
    {
        if (!IsBiometricLockEnabled || !IsBiometricAvailable)
        {
            IsSettingsLocked = false;
            return;
        }

        SttTestLog.Write("🔒 Ayarlar kilitli — Windows Hello doğrulaması isteniyor");
        // Kilit overlay'i doğrulamadan ÖNCE de aktif (boşluk/flaş yok)
        IsSettingsLocked = true;
        var verified = await BiometricService.VerifyAsync(
            "Ayarlara girmek için Windows Hello ile doğrulayın");
        IsSettingsLocked = !verified;
        if (!verified)
            SttTestLog.WriteWarning("✗ Doğrulama yapılmadı — sayfa kilitli kaldı");
    }

    [RelayCommand]
    private async Task LoadUserProfileAsync()
    {
        try
        {
            IsLoading = true;

            var user = _syncService.GetCurrentUser();
            if (user != null)
            {
                Email = user.Email ?? string.Empty;
                
                UserProfile = await _syncService.GetOrCreateProfileAsync();
                if (UserProfile != null)
                {
                    FullName = UserProfile.FullName ?? string.Empty;
                    SelectedLanguage = UserProfile.Preferences?.Language ?? "tr";
                    SelectedTheme = UserProfile.Preferences?.Theme ?? "light";
                    ThemeService.ApplyTheme(SelectedTheme);
                    EnableNotifications = UserProfile.Preferences?.EnableNotifications ?? true;
                    EnableVoiceRecording = UserProfile.Preferences?.EnableVoiceRecording ?? true;
                    AutoSync = UserProfile.Preferences?.AutoSync ?? true;
                    DefaultPriority = UserProfile.Preferences?.DefaultPriority ?? "medium";
                }

                // Ses efektleri tercihi cihazda saklanır (profil alanı değil)
                EnableSoundEffects = Preferences.Default.Get("enable_sounds", true);
                SoundEffectService.Enabled = EnableSoundEffects;
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Profil yüklenemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadUserStatsAsync()
    {
        try
        {
            UserStats = await _syncService.GetUserStatsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Stats loading failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        try
        {
            IsLoading = true;

            var updates = new
            {
                fullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim()
            };

            var updatedProfile = await _syncService.UpdateProfileAsync(updates);
            
            if (updatedProfile != null)
            {
                UserProfile = updatedProfile;
                SoundEffectService.Play(SoundEffectService.SoundKind.Success);
                await Shell.Current.DisplayAlert("Başarılı", "Profil güncellendi.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Profil güncellenemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SavePreferencesAsync()
    {
        try
        {
            IsLoading = true;

            var preferences = new Dictionary<string, object>
            {
                ["language"] = SelectedLanguage,
                ["theme"] = SelectedTheme,
                ["enableNotifications"] = EnableNotifications,
                ["enableVoiceRecording"] = EnableVoiceRecording,
                ["autoSync"] = AutoSync,
                ["defaultPriority"] = DefaultPriority
            };

            var updatedProfile = await _syncService.UpdateProfileAsync(new { preferences });
            
            if (updatedProfile != null)
            {
                ThemeService.SaveTheme(SelectedTheme);
                ThemeService.ApplyTheme(SelectedTheme);

                // Ses efektleri anında uygulanır + cihazda saklanır
                Preferences.Default.Set("enable_sounds", EnableSoundEffects);
                SoundEffectService.Enabled = EnableSoundEffects;
                SoundEffectService.Play(SoundEffectService.SoundKind.Success);

                await Shell.Current.DisplayAlert("Başarılı", "Tercihler kaydedildi.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Tercihler kaydedilemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        try
        {
            var success = await _syncService.SyncAllAsync();
            
            if (success)
            {
                SoundEffectService.Play(SoundEffectService.SoundKind.Success);
                await Shell.Current.DisplayAlert("Başarılı", "Senkronizasyon tamamlandı.", "Tamam");
                await LoadUserStatsAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Hata", "Senkronizasyon başarısız.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Senkronizasyon hatası: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private async Task ClearLocalDataAsync()
    {
        var result = await Shell.Current.DisplayAlert("Onay", "Tüm yerel veriler silinsin mi? Bu işlem geri alınamaz.", "Evet", "Hayır");
        
        if (result)
        {
            try
            {
                await _todoStore.ClearAllDataAsync();
                await Shell.Current.DisplayAlert("Başarılı", "Yerel veriler temizlendi.", "Tamam");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Veri temizleme başarısız: {ex.Message}", "Tamam");
            }
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var result = await Shell.Current.DisplayAlert("Onay", "Çıkış yapmak istediğinizden emin misiniz?", "Evet", "Hayır");
        
        if (result)
        {
            try
            {
                IsLoading = true;
                
                await _syncService.SignOutAsync();
                await _todoStore.ClearAllDataAsync();
                
                // Navigate to login page
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page =
                        new NavigationPage(new LoginPage(new LoginPageViewModel(_syncService, _todoStore)));
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Çıkış yapılamadı: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await Shell.Current.DisplayAlert("Hakkında", 
            "Todo Voice v1.0\n\n" +
            "Sesli görev yönetimi uygulaması\n" +
            "MiniMax tarafından geliştirilmiştir\n\n" +
            "Özellikler:\n" +
            "• Sesli görev ekleme\n" +
            "• Çevrimdışı destek\n" +
            "• Cross-platform uyumluluk\n" +
            "• Otomatik senkronizasyon", 
            "Tamam");
    }

    private void LoadSyncInfo()
    {
        IsOnline = _syncService.IsOnline;
        IsSyncing = _syncService.IsSyncing;
        LastSyncTime = _syncService.LastSyncTime;
    }

    private void OnSyncServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        LoadSyncInfo();
    }

    // ---- Ses Tanıma yardımcıları ----

    /// <summary>Seçili model bilgisi değiştiğinde (Picker) durum metnini tazele.</summary>
    partial void OnSelectedSttModelChanged(WhisperModelInfo? value)
    {
        RefreshSttStatus();
    }

    /// <summary>
    /// Sağlayıcı değiştiğinde: kaynağı uygula + anahtarı yükle (biyometrik kilit
    /// açıksa GİZLE — Windows Hello ile açılır) + bölgeyi yükle + görünürlükler.
    /// </summary>
    partial void OnSelectedSpeechProviderChanged(SpeechProviderInfo? value)
    {
        if (value != null)
        {
            _stt.SwitchProvider(value);
            _keyRevealed = !IsBiometricLockEnabled;
            ProviderApiKey = _keyRevealed ? CloudTranscribers.GetStoredApiKey(value.Id) : string.Empty;
            ProviderRegion = CloudTranscribers.GetStoredRegion(value.Id);
            if (string.IsNullOrEmpty(ProviderRegion) && !string.IsNullOrEmpty(value.DefaultRegion))
                ProviderRegion = value.DefaultRegion;
        }
        RefreshProviderStatus();
        OnPropertyChanged(nameof(IsOfflineProvider));
        OnPropertyChanged(nameof(IsApiKeyVisible));
        OnPropertyChanged(nameof(IsRegionRequired));
        OnPropertyChanged(nameof(ProviderDetailText));
        RefreshKeyLockState();
    }

    private void RefreshProviderStatus()
    {
        var p = SelectedSpeechProvider;
        if (p == null)
            return;

        if (p.Id == "offline")
        {
            ProviderStatusText = "Çevrimdışı — anahtar gerekmez, her zaman çalışır";
            return;
        }

        var hasKey = !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(p.Id));
        if (p.RequiresRegion && string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredRegion(p.Id)))
        {
            ProviderStatusText = hasKey
                ? "Bölge eksik — aşağıdan Azure bölgeni girip 'Anahtarı Kaydet'e bas."
                : "API anahtarı + bölge gerekli — ikisini de girip kaydet.";
        }
        else if (!hasKey)
        {
            ProviderStatusText = "API anahtarı girilmedi — bu kaynağı seçsen bile çevrimdışı Whisper kullanılır";
        }
        else
        {
            ProviderStatusText = "Anahtar kayıtlı. 'Bağlantıyı Test Et' ile doğrulayabilirsin";
        }
    }

    [RelayCommand]
    private void SaveProviderApiKey()
    {
        if (SelectedSpeechProvider is { Id: not "offline" } p)
        {
            _stt.SetProviderApiKey(p.Id, ProviderApiKey);
            CloudTranscribers.SaveRegion(p.Id, ProviderRegion);
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            RefreshProviderStatus();
            RefreshKeyLockState();
        }
    }

    [RelayCommand]
    private async Task TestProviderConnectionAsync()
    {
        var p = SelectedSpeechProvider;
        if (p == null)
            return;

        SttTestLog.Write($"──── Bağlantı testi: {p.DisplayName} ────");

        // Çevrimdışı Whisper testi: seçili modelle GERÇEK transkripsiyon çalıştır
        // (model yoksa önce indirilir — konsol satırlarıyla).
        if (p.Id == "offline")
        {
            // Büyük model henüz indirilmemişse test büyük indirme başlatabilir → onay iste
            if (!_stt.IsModelReady && _stt.SelectedModel.IsLargeModel)
            {
                var proceed = await Shell.Current.DisplayAlert(
                    "Model indirilecek",
                    $"Test için {_stt.SelectedModel.DisplayName} ({_stt.SelectedModel.SizeLabel}) indirilecek. " +
                    $"Bu birkaç dakika sürebilir. Devam edilsin mi?",
                    "İndir", "Vazgeç");
                if (!proceed)
                {
                    SttTestLog.WriteWarning("✗ Test iptal edildi (büyük model indirmesi onaylanmadı)");
                    return;
                }
            }

            IsProviderTesting = true;
            ProviderStatusText = "Çevrimdışı model test ediliyor…";
            try
            {
                var testWav = OpenAiCompatibleTranscriber.TestWavPath();
                SttTestLog.Write("Test WAV hazırlandı (0,2 sn sessizlik)");
                var text = await _stt.TranscribeOfflineAsync(testWav);
                var ok = text != null;
                ProviderStatusText = ok
                    ? $"Çevrimdışı model çalışıyor ✓ ({_stt.SelectedModel.DisplayName})"
                    : "Çevrimdışı model sonuç üretmedi (sessiz test sesi normal).";
                if (ok)
                    SttTestLog.WriteSuccess("✓ Çevrimdışı model testi başarılı");
                else
                    SttTestLog.WriteWarning("⚠ Çevrimdışı test boş sonuç (sessiz ses) — model çalışıyor olabilir");
                if (ok)
                    SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            }
            catch (Exception ex)
            {
                SttTestLog.WriteError($"✗ Çevrimdışı test hatası: {ex.Message}");
                ProviderStatusText = $"Test hatası: {ex.Message}";
            }
            finally
            {
                IsProviderTesting = false;
            }
            return;
        }

        // Biyometrik kilit açıksa önce Windows Hello doğrulaması iste — onaylanınca
        // anahtarı da GÖSTER (kilit açılınca ProviderApiKey boş kalmasın — bug)
        if (IsKeyLocked)
        {
            var verified = await BiometricService.VerifyAsync(
                $"{p.DisplayName} bağlantısını test etmek için Windows Hello ile doğrulayın");
            if (!verified)
            {
                ProviderStatusText = "Doğrulama yapılmadığı için test iptal edildi.";
                return;
            }
            _keyRevealed = true;
            ProviderApiKey = CloudTranscribers.GetStoredApiKey(p.Id);
            RefreshKeyLockState();
        }

        if (string.IsNullOrWhiteSpace(ProviderApiKey))
        {
            SttTestLog.WriteWarning($"✗ API anahtarı girilmedi ({p.Id})");
            await Shell.Current.DisplayAlert("Anahtar gerekli",
                $"{p.DisplayName} için API anahtarını önce girip kaydedin.", "Tamam");
            return;
        }

        IsProviderTesting = true;
        ProviderStatusText = "Bağlantı test ediliyor…";
        try
        {
            var ok = await _stt.TestProviderConnectionAsync(p.Id);
            ProviderStatusText = ok
                ? $"{p.DisplayName} bağlantısı başarılı ✓ — artık bu kaynak kullanılacak"
                : $"{p.DisplayName} bağlantısı başarısız. Anahtarı kontrol edin.";
            if (ok)
                SttTestLog.WriteSuccess("✓ Bağlantı testi başarılı");
            else
                SttTestLog.WriteError("✗ Bağlantı testi başarısız");
            if (ok)
                SoundEffectService.Play(SoundEffectService.SoundKind.Success);
        }
        catch (Exception ex)
        {
            SttTestLog.WriteError($"✗ Test hatası: {ex.Message}");
            ProviderStatusText = $"Test hatası: {ex.Message}";
        }
        finally
        {
            IsProviderTesting = false;
        }
    }

    private void RefreshSttStatus()
    {
        var model = SelectedSttModel;
        if (model == null)
            return;

        IsSttDownloading = _stt.IsDownloading;
        SttDownloadProgress = _stt.ModelDownloadProgress;
        SttDownloadPercent = (int)(SttDownloadProgress * 100);

        // İndirme detayları (modal + yeşil çubuk için)
        var down = _stt.ModelDownloadedBytes;
        var total = _stt.ModelDownloadTotalBytes;
        SttDownloadAmountText = total > 0
            ? $"{FormatBytes(down)} / {FormatBytes(total)}"
            : FormatBytes(down);
        SttDownloadSpeedText = _stt.ModelDownloadSpeedBytesPerSecond > 0
            ? $"{FormatBytes((long)_stt.ModelDownloadSpeedBytesPerSecond)}/sn"
            : string.Empty;
        SttDownloadFileLabel = _stt.SelectedModel.FileName;
        SttDownloadInlineText = total > 0
            ? $"İndiriliyor %{SttDownloadPercent} · {FormatBytes(down)}/{FormatBytes(total)} · detay için tıkla"
            : $"İndiriliyor %{SttDownloadPercent} · detay için tıkla";

        // Kurulu model diskte mi? (İndirme sürerken disk stat ÇAĞRILMAZ — her 80KB
        // chunk'ta binlerce gereksiz FileInfo sorgusu olmasın.)
        if (IsSttDownloading)
        {
            SttInstalledInfo = "İndiriliyor…";
        }
        else
        {
            var isInstalled = model.Id == _stt.SelectedModel.Id && _stt.IsModelReady;
            SttInstalledInfo = isInstalled
                ? $"Kurulu · {FormatBytes(_stt.SelectedModelSizeOnDisk)}"
                : model.Id == _stt.SelectedModel.Id
                    ? "Bu model seçili ama henüz indirilmedi"
                    : "İndirilmemiş";
        }

        SttStatusText = _stt.IsDownloading
            ? $"İndiriliyor: {model.DisplayName}… %{SttDownloadPercent}"
            : _stt.IsModelReady
                ? "Ses tanıma hazır — model kullanıma açık"
                : "İndirme başarısız oldu. İnterneti kontrol edip tekrar deneyin.";
    }

    private void OnSttPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpeechToTextService.IsDownloading)
            or nameof(SpeechToTextService.ModelDownloadProgress)
            or nameof(SpeechToTextService.ModelDownloadedBytes)
            or nameof(SpeechToTextService.ModelDownloadTotalBytes)
            or nameof(SpeechToTextService.ModelDownloadSpeedBytesPerSecond)
            or nameof(SpeechToTextService.IsModelReady))
        {
            RefreshSttStatus();
        }
    }

    /// <summary>Model Yönetimi popup'ının canlı kalması için dışarıya bildirir.</summary>
    public event EventHandler? ModelStateChanged;

    /// <summary>Model indirme/silme sonrası popup'ı tazele (header + satır durumları).</summary>
    public void NotifyModelStateChanged() => ModelStateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Yeşil ilerleme çubuğuna tıklayınca detaylı indirme modalını açar.</summary>
    [RelayCommand]
    private async Task ShowDownloadDetailsAsync()
    {
        if (!IsSttDownloading)
            return;

        var page = Shell.Current?.CurrentPage;
        if (page == null)
            return;

        var popup = new DownloadProgressPopup(this);
        await page.ShowPopupAsync(popup);
    }

    /// <summary>Modal içinden indirmeyi iptal eder.</summary>
    [RelayCommand]
    private void CancelSttDownload()
    {
        _stt.CancelModelDownload();
        SoundEffectService.Play(SoundEffectService.SoundKind.Delete);
    }

    // ---- Model yönetimi (Model Management popup) ----

    public WhisperModelInfo CurrentSttModel => _stt.SelectedModel;

    public bool IsSttModelInstalled(WhisperModelInfo model) => _stt.IsModelInstalled(model);

    public string GetSttModelDiskText(WhisperModelInfo model) => FormatBytes(_stt.GetModelSizeOnDisk(model));

    /// <summary>
    /// Model Yönetimi popup'ının kullandığı paylaşılan indirme akışı (büyük model
    /// onayı dahil). Başarı → true. Ayarlar'daki "İndir ve Kullan" butonu da bunu kullanır.
    /// </summary>
    public async Task<bool> EnsureSttModelAsync(WhisperModelInfo model, bool confirmLarge = true)
    {
        if (model == null || _stt.IsDownloading)
            return false;

        if (model.Id == _stt.SelectedModel.Id && _stt.IsModelReady)
            return true;

        if (confirmLarge && model.IsLargeModel)
        {
            var ok = await Shell.Current.DisplayAlert(
                "Büyük indirme",
                $"{model.DisplayName} modeli {model.SizeLabel}. Bu indirme birkaç dakika sürebilir " +
                $"ve {model.SizeLabel} disk alanı kaplar. Devam edilsin mi?",
                "İndir", "Vazgeç");
            if (!ok)
                return false;
        }

        SelectedSttModel = model;
        var success = await _stt.SwitchModelAsync(model);
        RefreshSttStatus();
        NotifyModelStateChanged();
        return success;
    }

    /// <summary>Kurulu bir modeli siler (aktif model silinemez — önce başka modele geç).</summary>
    public bool DeleteSttModel(WhisperModelInfo model)
    {
        var ok = _stt.DeleteModel(model);
        if (ok)
        {
            RefreshSttStatus();
            NotifyModelStateChanged();
        }
        return ok;
    }

    /// <summary>"Model Yönetimi" popup'ını açar — indir / sil / detaylı bilgi.</summary>
    [RelayCommand]
    private async Task ShowModelManagementAsync()
    {
        var page = Shell.Current?.CurrentPage;
        if (page == null)
            return;

        var popup = new ModelManagementPopup(_stt, this);
        await page.ShowPopupAsync(popup);
    }

    // ---- Biyometrik kilit (Windows Hello) ----

    private async Task RefreshBiometricStateAsync()
    {
        IsBiometricAvailable = await BiometricService.IsAvailableAsync();
        // Teşhis: unpackaged ortamda Windows Hello davranışını app.log'a yaz
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
                $"{DateTime.Now:HH:mm:ss} Biometric available={IsBiometricAvailable}{Environment.NewLine}");
        }
        catch { }
        BiometricStatusText = IsBiometricAvailable
            ? "Parmak izi / yüz / PIN ile API anahtarını koru."
            : "Windows Hello kullanılamıyor (kurulu değil veya bu sürümde desteklenmiyor). Anahtarlar yine de Windows Vault'ta şifreli.";

        // Kullanılabilirlik yoksa kilit zorla kapatılır (anahtar görünür kalır)
        if (!IsBiometricAvailable && IsBiometricLockEnabled)
        {
            IsBiometricLockEnabled = false;
            Preferences.Default.Set("stt_biometric_lock", false);
            _keyRevealed = true;
            if (SelectedSpeechProvider is { Id: not "offline" })
                ProviderApiKey = CloudTranscribers.GetStoredApiKey(SelectedSpeechProvider.Id);
        }
        RefreshKeyLockState();
    }

    private void RefreshKeyLockState()
    {
        var p = SelectedSpeechProvider;
        var hasKey = p != null && p.Id != "offline" &&
                     !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(p.Id));
        IsKeyLocked = IsBiometricLockEnabled && hasKey && !_keyRevealed;
    }

    /// <summary>Kilit overlay'indeki buton: Windows Hello ile sayfayı aç.</summary>
    [RelayCommand]
    private async Task UnlockSettingsAsync()
    {
        var verified = await BiometricService.VerifyAsync(
            "Ayarlara girmek için Windows Hello ile doğrulayın");
        if (verified)
        {
            IsSettingsLocked = false;
            SttTestLog.WriteSuccess("✓ Doğrulama başarılı — sayfa açıldı");
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
        }
    }

    /// <summary>Canlı konsolu temizler.</summary>
    [RelayCommand]
    private void ClearTestConsole()
    {
        _consoleLines.Clear();
        TestConsoleFormatted = new FormattedString();
    }

    /// <summary>API anahtarı gizle/göster (göz butonu).</summary>
    [RelayCommand]
    private void ToggleApiKeyVisibility() => IsApiKeyMasked = !IsApiKeyMasked;

    /// <summary>Ayarlar sayfasındaki Windows Hello anahtarını açar (Switch Toggled).</summary>
    public async Task SetBiometricLockAsync(bool enable)
    {
        if (enable == IsBiometricLockEnabled)
            return;

        if (enable)
        {
            if (!IsBiometricAvailable)
            {
                IsBiometricLockEnabled = false;
                return;
            }
            var ok = await BiometricService.VerifyAsync(
                "Windows Hello kilidini etkinleştirmek için kimliğinizi doğrulayın");
            if (!ok)
            {
                IsBiometricLockEnabled = false;
                return;
            }
            IsBiometricLockEnabled = true;
            _keyRevealed = false;
            ProviderApiKey = string.Empty; // ekrandaki anahtarı gizle
        }
        else
        {
            IsBiometricLockEnabled = false;
            _keyRevealed = true;
            if (SelectedSpeechProvider is { Id: not "offline" })
                ProviderApiKey = CloudTranscribers.GetStoredApiKey(SelectedSpeechProvider.Id);
        }

        Preferences.Default.Set("stt_biometric_lock", IsBiometricLockEnabled);
        RefreshKeyLockState();
        SoundEffectService.Play(IsBiometricLockEnabled
            ? SoundEffectService.SoundKind.Success
            : SoundEffectService.SoundKind.Click);
    }

    /// <summary>Kilitli anahtarı Windows Hello doğrulamasıyla açıp gösterir.</summary>
    [RelayCommand]
    private async Task UnlockApiKeyAsync()
    {
        if (!IsBiometricLockEnabled)
            return;

        var ok = await BiometricService.VerifyAsync(
            "API anahtarını görüntülemek için Windows Hello ile doğrulayın");
        if (!ok)
            return;

        _keyRevealed = true;
        if (SelectedSpeechProvider is { Id: not "offline" })
            ProviderApiKey = CloudTranscribers.GetStoredApiKey(SelectedSpeechProvider.Id);
        RefreshKeyLockState();
    }

    /// <summary>
    /// Singleton servislere (SyncService, SpeechToTextService) abone olan transient
    /// ViewModel her gezinmede yeniden oluşur — abonelikler çözülmezse singleton
    /// önceki VM örneklerini bellekte tutar. Sayfa kapanınca çözülür.
    /// </summary>
    public void Dispose()
    {
        _syncService.PropertyChanged -= OnSyncServicePropertyChanged;
        _stt.PropertyChanged -= OnSttPropertyChanged;
        SttTestLog.Line -= OnTestLogEntry;
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private async Task SwitchSttModelAsync()
    {
        var model = SelectedSttModel;
        if (model == null)
            return;

        if (_stt.IsDownloading)
        {
            await Shell.Current.DisplayAlert("İndirme sürüyor",
                "Başka bir model indiriliyor. Bittiğinde tekrar deneyin.", "Tamam");
            return;
        }

        if (model.Id == _stt.SelectedModel.Id && _stt.IsModelReady)
        {
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            await Shell.Current.DisplayAlert("Zaten kurulu", $"{model.DisplayName} modeli zaten hazır.", "Tamam");
            return;
        }

        SoundEffectService.Play(SoundEffectService.SoundKind.MicStart);
        var success = await EnsureSttModelAsync(model, confirmLarge: true);
        SoundEffectService.Play(success ? SoundEffectService.SoundKind.Success : SoundEffectService.SoundKind.Delete);

        if (success)
        {
            await Shell.Current.DisplayAlert("Tamam",
                $"{model.DisplayName} modeli hazır. Artık daha iyi ses tanıma kullanılacak.", "Tamam");
        }
        else
        {
            await Shell.Current.DisplayAlert("Hata",
                "Model indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.", "Tamam");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        return $"{bytes / 1024.0:0} KB";
    }

    // Language options
    public List<KeyValuePair<string, string>> LanguageOptions { get; } = new()
    {
        new("tr", "Türkçe"),
        new("en", "English")
    };

    // Theme options
    public List<KeyValuePair<string, string>> ThemeOptions { get; } = new()
    {
        new("light", "Açık"),
        new("dark", "Koyu"),
        new("system", "Sistem")
    };

    // Priority options
    public List<KeyValuePair<string, string>> PriorityOptions { get; } = new()
    {
        new("low", "Düşük"),
        new("medium", "Orta"),
        new("high", "Yüksek")
    };

    // Computed properties
    public string SyncStatusText => IsOnline ? "Çevrimiçi" : "Çevrimdışı";
    public string LastSyncText => LastSyncTime > DateTime.MinValue ? $"Son senkron: {LastSyncTime:dd.MM.yyyy HH:mm}" : "Hiç senkronize edilmedi";
}