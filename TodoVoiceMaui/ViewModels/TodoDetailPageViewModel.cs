using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.ViewModels;

[QueryProperty(nameof(Todo), "Todo")]
public partial class TodoDetailPageViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly AudioService _audioService;
    private readonly SupabaseService _supabaseService;

    [ObservableProperty]
    private Todo? todo;

    [ObservableProperty]
    private ObservableCollection<VoiceRecording> voiceRecordings = new();

    partial void OnVoiceRecordingsChanged(ObservableCollection<VoiceRecording> value)
    {
        value.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasVoiceRecordings));
        };
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string priority = "medium";

    [ObservableProperty]
    private DateTime? dueDate;

    [ObservableProperty]
    private DateTime? reminderDate;

    [ObservableProperty]
    private DateTime editDueDate = DateTime.Today;

    [ObservableProperty]
    private DateTime editReminderDate = DateTime.Today;

    [ObservableProperty]
    private bool completed = false;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isEditing = false;

    [ObservableProperty]
    private bool isRecording = false;

    [ObservableProperty]
    private bool isPlaying = false;

    [ObservableProperty]
    private string recordingDuration = "00:00";

    [ObservableProperty]
    private VoiceRecording? currentlyPlaying;

    private string? _pendingVoiceFilePath;

    public TodoDetailPageViewModel(SyncService syncService, AudioService audioService, SupabaseService supabaseService)
    {
        _syncService = syncService;
        _audioService = audioService;
        _supabaseService = supabaseService;

        // Subscribe to audio service events
        _audioService.PropertyChanged += OnAudioServicePropertyChanged;
        _audioService.RecordingCompleted += OnRecordingCompleted;
        _audioService.RecordingError += OnRecordingError;
        _audioService.PlaybackCompleted += OnPlaybackCompleted;
        _audioService.PlaybackError += OnPlaybackError;
        _audioService.PlaybackPositionUpdated += OnPlaybackPositionUpdated;
    }

    partial void OnTodoChanged(Todo? value)
    {
        if (value != null)
        {
            LoadTodoData();
            _ = LoadVoiceRecordingsAsync();
        }
    }

    private void LoadTodoData()
    {
        if (Todo == null) return;

        Title = Todo.Title;
        Description = Todo.Description ?? string.Empty;
        Priority = Todo.Priority;
        DueDate = Todo.DueDate;
        ReminderDate = Todo.ReminderAt;
        EditDueDate = Todo.DueDate ?? DateTime.Today;
        EditReminderDate = Todo.ReminderAt ?? DateTime.Today.AddDays(1);
        Completed = Todo.Completed;
    }

    [RelayCommand]
    private async Task LoadVoiceRecordingsAsync()
    {
        if (Todo == null) return;

        try
        {
            IsLoading = true;

            var (todoDetail, recordings) = await _supabaseService.GetTodoWithVoiceAsync(Todo.Id);

            VoiceRecordings.Clear();
            if (recordings != null)
            {
                foreach (var recording in recordings.OrderByDescending(r => r.CreatedAt))
                {
                    VoiceRecordings.Add(recording);
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Ses kayıtları yüklenemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void StartEditing()
    {
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (Todo == null) return;

        try
        {
            IsLoading = true;

            DueDate = EditDueDate;
            ReminderDate = EditReminderDate;

            var updates = new
            {
                title = Title.Trim(),
                description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                priority = Priority,
                dueDate = DueDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                reminderAt = ReminderDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                completed = Completed
            };

            var success = await _syncService.UpdateTodoAsync(Todo.Id, updates);

            if (success)
            {
                // Update local todo object
                Todo.Title = Title.Trim();
                Todo.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
                Todo.Priority = Priority;
                Todo.DueDate = DueDate;
                Todo.ReminderAt = ReminderDate;
                Todo.Completed = Completed;
                Todo.UpdatedAt = DateTime.UtcNow;

                IsEditing = false;
                
                await Shell.Current.DisplayAlert("Başarılı", "Değişiklikler kaydedildi.", "Tamam");
            }
            else
            {
                await Shell.Current.DisplayAlert("Hata", "Değişiklikler kaydedilemedi. Lütfen tekrar deneyin.", "Tamam");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Kaydetme başarısız: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CancelEditing()
    {
        LoadTodoData();
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteTodoAsync()
    {
        if (Todo == null) return;

        var result = await Shell.Current.DisplayAlert("Onay", $"'{Todo.Title}' görevi silinsin mi?", "Evet", "Hayır");

        if (result)
        {
            try
            {
                IsLoading = true;
                
                var success = await _syncService.DeleteTodoAsync(Todo.Id);

                if (success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", "Görev silinemedi. Lütfen tekrar deneyin.", "Tamam");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Silme başarısız: {ex.Message}", "Tamam");
            }
            finally
            {
                IsLoading = false;
            }
        }
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
    private async Task SaveVoiceRecordingAsync()
    {
        if (Todo == null || string.IsNullOrEmpty(_pendingVoiceFilePath)) return;

        try
        {
            IsLoading = true;

            var audioData = await _audioService.GetRecordingDataAsync(_pendingVoiceFilePath);
            if (audioData != null)
            {
                var base64Data = _audioService.ConvertToBase64(audioData);
                var fileName = $"todo_{Todo.Id}_voice_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                var duration = (int)_audioService.RecordingDuration.TotalSeconds;

                var uploadedRecording = await _supabaseService.UploadVoiceRecordingAsync(
                    base64Data, fileName, Todo.Id, duration);

                if (uploadedRecording != null)
                {
                    VoiceRecordings.Insert(0, uploadedRecording);
                    
                    // Update todo with voice recording URL if it's the first one
                    if (string.IsNullOrEmpty(Todo.VoiceRecordingUrl))
                    {
                        await _syncService.UpdateTodoAsync(Todo.Id, new 
                        { 
                            voice_recording_url = uploadedRecording.FileUrl,
                            voice_duration = duration 
                        });
                        
                        Todo.VoiceRecordingUrl = uploadedRecording.FileUrl;
                        Todo.VoiceDuration = duration;
                    }

                    ClearRecording();
                    await Shell.Current.DisplayAlert("Başarılı", "Ses kaydı kaydedildi.", "Tamam");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Hata", "Ses kaydı yüklenemedi.", "Tamam");
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Ses kaydı kaydedilemedi: {ex.Message}", "Tamam");
        }
        finally
        {
            IsLoading = false;
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
        
        RecordingDuration = "00:00";
    }

    [RelayCommand]
    private async Task PlayVoiceRecordingAsync(VoiceRecording recording)
    {
        try
        {
            if (IsPlaying && CurrentlyPlaying?.Id == recording.Id)
            {
                recording.IsPlaying = false;
                _audioService.StopPlayback();
            }
            else
            {
                if (IsPlaying && CurrentlyPlaying != null)
                {
                    CurrentlyPlaying.IsPlaying = false;
                    _audioService.StopPlayback();
                }

                if (!string.IsNullOrEmpty(recording.FileUrl))
                {
                    var started = await _audioService.PlayRecordingFromUrlAsync(recording.FileUrl);
                    if (started)
                    {
                        recording.PlaybackProgress = 0;
                        recording.IsPlaying = true;
                        CurrentlyPlaying = recording;
                    }
                    else
                    {
                        await Shell.Current.DisplayAlert("Hata", "Ses dosyası oynatılamadı.", "Tamam");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Hata", $"Oynatma hatası: {ex.Message}", "Tamam");
        }
    }

    [RelayCommand]
    private async Task DeleteVoiceRecordingAsync(VoiceRecording recording)
    {
        var result = await Shell.Current.DisplayAlert("Onay", "Bu ses kaydı silinsin mi?", "Evet", "Hayır");

        if (result)
        {
            try
            {
                // Note: In a full implementation, you would call a delete edge function
                // For now, we'll just remove from the UI
                VoiceRecordings.Remove(recording);

                await Shell.Current.DisplayAlert("Başarılı", "Ses kaydı silindi.", "Tamam");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Hata", $"Silme başarısız: {ex.Message}", "Tamam");
            }
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (IsEditing)
        {
            var result = await Shell.Current.DisplayAlert("Onay", "Kaydedilmemiş değişiklikler kaybolacak. Çıkmak istiyor musunuz?", "Evet", "Hayır");
            if (!result) return;
        }

        await Shell.Current.GoToAsync("..");
    }

    private void OnAudioServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioService.IsRecording))
        {
            IsRecording = _audioService.IsRecording;
        }
        else if (e.PropertyName == nameof(AudioService.IsPlaying))
        {
            IsPlaying = _audioService.IsPlaying;
            if (!IsPlaying)
            {
                if (CurrentlyPlaying != null)
                {
                    CurrentlyPlaying.IsPlaying = false;
                }
                CurrentlyPlaying = null;
            }
        }
        else if (e.PropertyName == nameof(AudioService.RecordingDuration))
        {
            RecordingDuration = _audioService.RecordingDuration.ToString(@"mm\:ss");
        }
    }

    private void OnPlaybackPositionUpdated(object? sender, TimeSpan position)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentlyPlaying != null)
            {
                var duration = _audioService.PlaybackDuration.TotalSeconds;
                if (duration > 0)
                {
                    CurrentlyPlaying.PlaybackProgress = Math.Clamp(position.TotalSeconds / duration, 0, 1);
                }
            }
        });
    }

    private void OnRecordingCompleted(object? sender, string filePath)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _pendingVoiceFilePath = filePath;
        });
    }

    private void OnRecordingError(object? sender, Exception error)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.DisplayAlert("Ses Kayıt Hatası", error.Message, "Tamam");
        });
    }

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentlyPlaying != null)
            {
                CurrentlyPlaying.IsPlaying = false;
                CurrentlyPlaying.PlaybackProgress = 0;
            }
            CurrentlyPlaying = null;
        });
    }

    private void OnPlaybackError(object? sender, Exception error)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (CurrentlyPlaying != null)
            {
                CurrentlyPlaying.IsPlaying = false;
            }
            CurrentlyPlaying = null;
            await Shell.Current.DisplayAlert("Oynatma Hatası", error.Message, "Tamam");
        });
    }

    // Computed properties
    public string RecordingButtonText => IsRecording ? "Dur" : "Kaydet";
    public string RecordingButtonIcon => IsRecording ? "⏹️" : "🎤";
    public bool HasPendingVoice => !string.IsNullOrEmpty(_pendingVoiceFilePath);
    public bool CanSaveVoice => HasPendingVoice && !IsLoading;
    public bool HasVoiceRecordings => VoiceRecordings.Count > 0;

    public string GetPlayButtonIcon(VoiceRecording recording)
    {
        return IsPlaying && CurrentlyPlaying?.Id == recording.Id ? "⏸️" : "▶️";
    }
}