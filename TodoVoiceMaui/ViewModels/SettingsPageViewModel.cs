using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.Views;

namespace TodoVoiceMaui.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly ITodoStore _todoStore;

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
    private string defaultPriority = "medium";

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isSyncing = false;

    [ObservableProperty]
    private DateTime lastSyncTime;

    [ObservableProperty]
    private bool isOnline = true;

    public SettingsPageViewModel(SyncService syncService, ITodoStore todoStore)
    {
        _syncService = syncService;
        _todoStore = todoStore;

        // Subscribe to sync service
        _syncService.PropertyChanged += OnSyncServicePropertyChanged;
    }

    public async Task InitializeAsync()
    {
        await LoadUserProfileAsync();
        await LoadUserStatsAsync();
        LoadSyncInfo();
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