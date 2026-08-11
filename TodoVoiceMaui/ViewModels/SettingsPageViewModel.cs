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

    public IReadOnlyList<WhisperModelInfo> SttModels { get; } = WhisperModelCatalog.All;

    public SettingsPageViewModel(SyncService syncService, ITodoStore todoStore, SpeechToTextService stt)
    {
        _syncService = syncService;
        _todoStore = todoStore;
        _stt = stt;

        // Subscribe to sync service
        _syncService.PropertyChanged += OnSyncServicePropertyChanged;

        // STT model durumunu canlı tut (indirme ilerlemesi arayüze yansır)
        _stt.PropertyChanged += OnSttPropertyChanged;
        SelectedSttModel = _stt.SelectedModel;
    }

    public async Task InitializeAsync()
    {
        await LoadUserProfileAsync();
        await LoadUserStatsAsync();
        LoadSyncInfo();
        RefreshSttStatus();
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

    private void RefreshSttStatus()
    {
        var model = SelectedSttModel;
        if (model == null)
            return;

        IsSttDownloading = _stt.IsDownloading;
        SttDownloadProgress = _stt.ModelDownloadProgress;

        // Kurulu model diskte mi?
        var isInstalled = model.Id == _stt.SelectedModel.Id && _stt.IsModelReady;
        SttInstalledInfo = isInstalled
            ? $"Kurulu · {FormatBytes(_stt.SelectedModelSizeOnDisk)}"
            : model.Id == _stt.SelectedModel.Id
                ? "Bu model seçili ama henüz indirilmedi"
                : "İndirilmemiş";

        SttStatusText = _stt.IsDownloading
            ? $"İndiriliyor: {model.DisplayName}… %{(int)(SttDownloadProgress * 100)}"
            : _stt.IsModelReady
                ? "Ses tanıma hazır — model kullanıma açık"
                : "İndirme başarısız oldu. İnterneti kontrol edip tekrar deneyin.";
    }

    private void OnSttPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpeechToTextService.IsDownloading)
            or nameof(SpeechToTextService.ModelDownloadProgress)
            or nameof(SpeechToTextService.IsModelReady))
        {
            RefreshSttStatus();
        }
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

        var alreadyInstalled = model.Id == _stt.SelectedModel.Id && _stt.IsModelReady;
        if (alreadyInstalled)
        {
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            await Shell.Current.DisplayAlert("Zaten kurulu", $"{model.DisplayName} modeli zaten hazır.", "Tamam");
            return;
        }

        // Büyük model (1GB+) için kullanıcı onayı
        if (model.IsLargeModel)
        {
            var ok = await Shell.Current.DisplayAlert(
                "Büyük indirme",
                $"{model.DisplayName} modeli {model.SizeLabel}. Bu indirme birkaç dakika sürebilir " +
                $"ve {model.SizeLabel} disk alanı kaplar. Devam edilsin mi?",
                "İndir", "Vazgeç");
            if (!ok)
                return;
        }

        try
        {
            IsSttDownloading = true;
            SoundEffectService.Play(SoundEffectService.SoundKind.MicStart);

            var success = await _stt.SwitchModelAsync(model);
            if (success)
            {
                SoundEffectService.Play(SoundEffectService.SoundKind.Success);
                await Shell.Current.DisplayAlert("Tamam",
                    $"{model.DisplayName} modeli hazır. Artık daha iyi ses tanıma kullanılacak.", "Tamam");
            }
            else
            {
                await Shell.Current.DisplayAlert("Hata",
                    "Model indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Model değiştirilemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsSttDownloading = false;
            RefreshSttStatus();
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