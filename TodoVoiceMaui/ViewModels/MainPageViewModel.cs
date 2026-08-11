using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Services;

namespace TodoVoiceMaui.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly ITodoStore _todoStore;

    [ObservableProperty]
    private string welcomeMessage = "Todo Voice'a Hoş Geldiniz";

    [ObservableProperty]
    private string userDisplayName = "Kullanıcı";

    [ObservableProperty]
    private int totalTodos;

    [ObservableProperty]
    private int completedTodos;

    [ObservableProperty]
    private int pendingTodos;

    [ObservableProperty]
    private int voiceTodos;

    [ObservableProperty]
    private bool isLoadingStats;

    [ObservableProperty]
    private double progressRatio;

    [ObservableProperty]
    private string progressLabel = "Henüz görev yok";

    [ObservableProperty]
    private string todayLabel = DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));

    public MainPageViewModel(SyncService syncService, ITodoStore todoStore)
    {
        _syncService = syncService;
        _todoStore = todoStore;
        LoadUserInfo();
    }

    private void LoadUserInfo()
    {
        var user = _syncService.GetCurrentUser();
        if (user != null)
        {
            UserDisplayName = user.Email?.Split('@')[0] ?? "Kullanıcı";
            WelcomeMessage = $"Hoş geldiniz, {UserDisplayName}!";
        }
    }

    [RelayCommand]
    private async Task NavigateToTodosAsync()
    {
        SoundEffectService.Play(SoundEffectService.SoundKind.Click);
        await Shell.Current.GoToAsync("//todos");
    }

    [RelayCommand]
    private async Task NavigateToSettingsAsync()
    {
        SoundEffectService.Play(SoundEffectService.SoundKind.Click);
        await Shell.Current.GoToAsync("//settings");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadStatsAsync();
    }

    public async Task LoadStatsAsync()
    {
        if (IsLoadingStats) return;

        try
        {
            IsLoadingStats = true;
            var todos = await _todoStore.GetTodosAsync();
            var todosWithVoice = await _todoStore.GetVoiceRecordingsAsync();

            TotalTodos = todos.Count;
            CompletedTodos = todos.Count(t => t.Completed);
            PendingTodos = todos.Count(t => !t.Completed);
            VoiceTodos = todosWithVoice.Count;

            ProgressRatio = TotalTodos > 0 ? (double)CompletedTodos / TotalTodos : 0;
            ProgressLabel = TotalTodos > 0
                ? $"{CompletedTodos}/{TotalTodos} tamamlandı"
                : "Henüz görev yok";
        }
        finally
        {
            IsLoadingStats = false;
        }
    }
}