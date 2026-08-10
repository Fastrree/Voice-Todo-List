using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.Views;
using TodoVoiceMaui.Core.Application.Voice;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.ViewModels;

public partial class TodoListPageViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly AudioService _audioService;
    private readonly SupabaseService _supabaseService;
    private readonly SpeechToTextService _speechToTextService;
    private readonly IVoiceCommandParser _voiceCommandParser;
    private readonly IVoiceCommandHandler _voiceCommandHandler;

    [ObservableProperty]
    private ObservableCollection<TodoListItem> todos = new();

    [ObservableProperty]
    private ObservableCollection<TodoListItem> filteredTodos = new();

    [ObservableProperty]
    private string newTodoTitle = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isSyncing = false;

    [ObservableProperty]
    private bool isRecording = false;

    [ObservableProperty]
    private bool hasRecording = false;

    [ObservableProperty]
    private string recordingDuration = "00:00";

    [ObservableProperty]
    private string selectedFilter = "all";

    [ObservableProperty]
    private string selectedPriority = "all";

    [ObservableProperty]
    private string selectedSort = "created_desc";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isOnline = true;

    [ObservableProperty]
    private DateTime lastSyncTime = DateTime.MinValue;

    [ObservableProperty]
    private string syncStatus = "Senkronize edildi";

    [ObservableProperty]
    private VoiceFlowState voiceFlowState = VoiceFlowState.Idle;

    [ObservableProperty]
    private string liveTranscript = string.Empty;

    private string? _pendingVoiceFilePath;

    public TodoListPageViewModel(SyncService syncService, AudioService audioService, SupabaseService supabaseService, SpeechToTextService speechToTextService, IVoiceCommandParser voiceCommandParser, IVoiceCommandHandler voiceCommandHandler)
    {
        _syncService = syncService;
        _audioService = audioService;
        _supabaseService = supabaseService;
        _speechToTextService = speechToTextService;
        _voiceCommandParser = voiceCommandParser;
        _voiceCommandHandler = voiceCommandHandler;
        // Subscribe to service events
        _syncService.PropertyChanged += OnSyncServicePropertyChanged;
        _syncService.SyncProgress += OnSyncProgress;
        _syncService.SyncCompleted += OnSyncCompleted;

        _audioService.PropertyChanged += OnAudioServicePropertyChanged;
        _audioService.RecordingCompleted += OnRecordingCompleted;
        _audioService.RecordingError += OnRecordingError;

        _speechToTextService.TranscriptionCompleted += OnTranscriptionCompleted;
        _speechToTextService.SpeechError += OnSpeechError;
        _speechToTextService.PropertyChanged += OnSpeechToTextPropertyChanged;
    }

    public async Task InitializeAsync()
    {
        await LoadTodosAsync();
    }

    [RelayCommand]
    private async Task LoadTodosAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            var todoList = await _syncService.GetTodosAsync();
            
            Todos.Clear();
            foreach (var todo in todoList.OrderByDescending(t => t.CreatedAt))
            {
                Todos.Add(new TodoListItem(todo));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Görevler yüklenemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await _syncService.SyncAllAsync();
            await LoadTodosAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Yenileme başarısız: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private async Task AddTodoAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle))
        {
            await Shell.Current.DisplayAlert("Uyarı", "Görev başlığı boş olamaz.", "Tamam");
            return;
        }

        try
        {
            IsLoading = true;

            // Handle voice recording if exists
            string? voiceUrl = null;
            int? voiceDuration = null;

            if (HasRecording && !string.IsNullOrEmpty(_pendingVoiceFilePath))
            {
                try
                {
                    var audioData = await _audioService.GetRecordingDataAsync(_pendingVoiceFilePath);
                    if (audioData != null)
                    {
                        var base64Data = _audioService.ConvertToBase64(audioData);
                        var fileName = $"todo_voice_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                        
                        var uploadedRecording = await _supabaseService.UploadVoiceRecordingAsync(base64Data, fileName, duration: (int)_audioService.RecordingDuration.TotalSeconds);
                        
                        if (uploadedRecording != null)
                        {
                            voiceUrl = uploadedRecording.FileUrl;
                            voiceDuration = uploadedRecording.Duration;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Voice upload failed: {ex.Message}");
                    // Continue with todo creation without voice
                }
                finally
                {
                    ClearRecording();
                }
            }

            var success = await _syncService.CreateTodoAsync(NewTodoTitle.Trim());

            if (success)
            {
                NewTodoTitle = string.Empty;
                await LoadTodosAsync();
            }
            else
            {
                await Shell.Current.DisplayAlert("Hata", "Görev eklenemedi. Lütfen tekrar deneyin.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Görev eklenemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleTodoAsync(TodoListItem todo)
    {
        try
        {
            await _syncService.UpdateTodoAsync(todo.Id, new { completed = !todo.Completed });
            todo.Completed = !todo.Completed;
            
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Görev güncellenemedi: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private async Task DeleteTodoAsync(TodoListItem todo)
    {
        var result = await Shell.Current.DisplayAlert("Onay", $"'{todo.Title}' görevi silinsin mi?", "Evet", "Hayır");
        
        if (result)
        {
            try
            {
                await _syncService.DeleteTodoAsync(todo.Id);
                Todos.Remove(todo);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Görev silinemedi: {ex.Message}", "Tamam");
            }
        }
    }

    [RelayCommand]
    private async Task OpenTodoDetailAsync(TodoListItem todo)
    {
        var parameters = new Dictionary<string, object>
        {
            { "Todo", todo.Model }
        };
        
        await Shell.Current.GoToAsync($"{nameof(TodoDetailPage)}", parameters);
    }

    [RelayCommand]
    private async Task StartSpeechToTextAsync()
    {
        if (VoiceFlowState == VoiceFlowState.Listening)
        {
            await StopSpeechToTextAsync();
            return;
        }

        if (!_speechToTextService.IsAvailable)
        {
            await Shell.Current.DisplayAlert("Hata", "Ses tanıma bu cihazda kullanılamıyor.", "Tamam");
            return;
        }

        try
        {
            VoiceFlowState = VoiceFlowState.Listening;
            LiveTranscript = string.Empty;

            var started = await _speechToTextService.StartListeningAsync();
            if (!started)
            {
                VoiceFlowState = VoiceFlowState.Failed;
            }
        }
        catch (Exception ex)
        {
            VoiceFlowState = VoiceFlowState.Failed;
            await Shell.Current.DisplayAlert("Hata", $"Ses tanıma hatası: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private async Task StopSpeechToTextAsync()
    {
        await _speechToTextService.StopListeningAsync();
        VoiceFlowState = VoiceFlowState.Processing;
        LiveTranscript = string.Empty;
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        try
        {
            if (IsRecording)
            {
                var filePath = await _audioService.StopRecordingAsync();
                _pendingVoiceFilePath = filePath;
            }
            else
            {
                var started = await _audioService.StartRecordingAsync();
                if (!started)
                {
                    await Shell.Current.DisplayAlert("Hata", "Ses kaydı başlatılamadı. Mikrofon iznini kontrol edin.", "Tamam");
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Ses kaydı hatası: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private void ClearRecording()
    {
        if (!string.IsNullOrEmpty(_pendingVoiceFilePath))
        {
            _audioService.DeleteRecording(_pendingVoiceFilePath);
            _pendingVoiceFilePath = null;
        }
        
        HasRecording = false;
        RecordingDuration = "00:00";
    }

    [RelayCommand]
    private void ChangeFilter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private void ChangePriority(string priority)
    {
        SelectedPriority = priority;
        ApplyFilter();
    }

    [RelayCommand]
    private void ChangeSort(string sort)
    {
        SelectedSort = sort;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTodos.Clear();

        var filtered = SelectedFilter switch
        {
            "completed" => Todos.Where(t => t.Completed),
            "pending" => Todos.Where(t => !t.Completed),
            "with_voice" => Todos.Where(t => t.HasVoiceRecording),
            _ => Todos
        };

        if (SelectedPriority != "all")
        {
            filtered = filtered.Where(t => t.Priority == SelectedPriority);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(t => 
                t.Title.ToLowerInvariant().Contains(search) || 
                (t.Description?.ToLowerInvariant().Contains(search) ?? false));
        }

        var ordered = SelectedSort switch
        {
            "created_asc" => filtered.OrderBy(t => t.CreatedAt),
            "due_date" => filtered.OrderBy(t => t.DueDate ?? DateTime.MaxValue),
            "priority" => filtered.OrderBy(t => PriorityRank(t.Priority)).ThenBy(t => t.CreatedAt),
            _ => filtered.OrderByDescending(t => t.CreatedAt)
        };

        foreach (var todo in ordered)
        {
            FilteredTodos.Add(todo);
        }
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3
    };

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();
    partial void OnSelectedPriorityChanged(string value) => ApplyFilter();
    partial void OnSelectedSortChanged(string value) => ApplyFilter();

    public List<KeyValuePair<string, string>> FilterOptions { get; } = new()
    {
        new("all", "Tümü"),
        new("pending", "Bekleyen"),
        new("completed", "Tamamlanan"),
        new("with_voice", "Sesli")
    };

    public List<KeyValuePair<string, string>> PriorityFilterOptions { get; } = new()
    {
        new("all", "Tüm Öncelikler"),
        new("high", "Yüksek"),
        new("medium", "Orta"),
        new("low", "Düşük")
    };

    public List<KeyValuePair<string, string>> SortOptions { get; } = new()
    {
        new("created_desc", "En Yeni"),
        new("created_asc", "En Eski"),
        new("due_date", "Teslim Tarihi"),
        new("priority", "Öncelik")
    };

    private void OnSyncServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SyncService.IsSyncing))
        {
            IsSyncing = _syncService.IsSyncing;
        }
        else if (e.PropertyName == nameof(SyncService.IsOnline))
        {
            IsOnline = _syncService.IsOnline;
            SyncStatus = IsOnline ? "Çevrimiçi" : "Çevrimdışı";
        }
        else if (e.PropertyName == nameof(SyncService.LastSyncTime))
        {
            LastSyncTime = _syncService.LastSyncTime;
            if (LastSyncTime > DateTime.MinValue)
            {
                SyncStatus = $"Son senkron: {LastSyncTime:HH:mm}";
            }
        }
    }

    private void OnSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncStatus = e.Message;
        });
    }

    private void OnSyncCompleted(object? sender, SyncCompletedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncStatus = e.Success ? "Senkronize edildi" : "Senkron hatası";
            if (e.Success)
            {
                _ = LoadTodosAsync();
            }
        });
    }

    private void OnAudioServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioService.IsRecording))
        {
            IsRecording = _audioService.IsRecording;
        }
        else if (e.PropertyName == nameof(AudioService.RecordingDuration))
        {
            RecordingDuration = _audioService.RecordingDuration.ToString(@"mm\:ss");
        }
        else if (e.PropertyName == nameof(AudioService.HasRecording))
        {
            HasRecording = _audioService.HasRecording;
        }
    }

    private void OnRecordingCompleted(object? sender, string filePath)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pendingVoiceFilePath = filePath;
            HasRecording = true;
        });
    }

    private void OnRecordingError(object? sender, Exception error)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.DisplayAlert("Ses Kayıt Hatası", error.Message, "Tamam");
        });
    }

    private void OnSpeechToTextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpeechToTextService.LiveTranscript))
        {
            LiveTranscript = _speechToTextService.LiveTranscript;
        }
    }

    private void OnTranscriptionCompleted(object? sender, string text)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            VoiceFlowState = VoiceFlowState.Processing;
            LiveTranscript = string.Empty;

            try
            {
                var transcription = new TranscriptionResult(
                    text,
                    TranscriptionConfidence.Medium,
                    provider: "windows-speech");

                var command = _voiceCommandParser.Parse(transcription);
                var result = await _voiceCommandHandler.HandleAsync(command);

                if (result.Success)
                {
                    VoiceFlowState = VoiceFlowState.Recognized;
                    NewTodoTitle = string.Empty;
                    await LoadTodosAsync();
                }
                else
                {
                    VoiceFlowState = VoiceFlowState.Failed;
                    await Shell.Current.DisplayAlert("Ses Komutu", result.Message ?? "Komut işlenemedi.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                VoiceFlowState = VoiceFlowState.Failed;
                System.Diagnostics.Debug.WriteLine($"Voice command failed: {ex.Message}");
                await Shell.Current.DisplayAlert("Hata", $"Komut işlenirken hata oluştu: {ex.Message}", "Tamam");
            }
            finally
            {
                VoiceFlowState = VoiceFlowState.Idle;
            }
        });
    }

    private void OnSpeechError(object? sender, Exception error)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            VoiceFlowState = VoiceFlowState.Failed;
            LiveTranscript = string.Empty;
            await Shell.Current.DisplayAlert("Ses Tanıma", error.Message, "Tamam");
            VoiceFlowState = VoiceFlowState.Idle;
        });
    }

    // Computed properties
    public string RecordingButtonText => IsRecording ? "Dur" : "Kaydet";
    public string RecordingButtonIcon => IsRecording ? "⏹️" : "🎤";
    public int TotalTodosCount => Todos.Count;
    public int CompletedTodosCount => Todos.Count(t => t.Completed);
    public int PendingTodosCount => Todos.Count(t => !t.Completed);
    public int VoiceTodosCount => Todos.Count(t => t.HasVoiceRecording);

    // Voice flow — UI state derived from the single Core source of truth
    public bool IsSpeechListening => VoiceFlowState == VoiceFlowState.Listening;

    public string SpeechStatus => VoiceFlowState switch
    {
        VoiceFlowState.Listening => "Dinliyor... konuşun",
        VoiceFlowState.Processing => "İşleniyor...",
        VoiceFlowState.Recognized => "✓ Tanındı",
        VoiceFlowState.Failed => "Anlaşılamadı",
        _ => string.Empty
    };

    partial void OnVoiceFlowStateChanged(VoiceFlowState value)
    {
        OnPropertyChanged(nameof(IsSpeechListening));
        OnPropertyChanged(nameof(SpeechStatus));
    }
}