using System.ComponentModel;
using System.Runtime.CompilerServices;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.Services;

public class SyncService : INotifyPropertyChanged
{
    private readonly SupabaseService _supabaseService;
    private readonly ITodoStore _todoStore;
    private readonly AudioService _audioService;
    private bool _isSyncing;
    private bool _isOnline = true;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private Timer? _syncTimer;

    public SyncService(SupabaseService supabaseService, ITodoStore todoStore, AudioService audioService)
    {
        _supabaseService = supabaseService;
        _todoStore = todoStore;
        _audioService = audioService;

        // Setup connectivity monitoring
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        _isOnline = Connectivity.NetworkAccess == NetworkAccess.Internet;

        // Setup periodic sync
        StartPeriodicSync();
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set => SetProperty(ref _isSyncing, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        private set => SetProperty(ref _isOnline, value);
    }

    public DateTime LastSyncTime
    {
        get => _lastSyncTime;
        private set => SetProperty(ref _lastSyncTime, value);
    }

    public event EventHandler<SyncProgressEventArgs>? SyncProgress;
    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<Exception>? SyncError;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var wasOnline = IsOnline;
        IsOnline = e.NetworkAccess == NetworkAccess.Internet;

        // If we just came back online, sync immediately
        if (!wasOnline && IsOnline)
        {
            _ = Task.Run(SyncAllAsync);
        }
    }

    private void StartPeriodicSync()
    {
        // Sync every 5 minutes when online
        _syncTimer = new Timer(async _ =>
        {
            if (IsOnline && !IsSyncing)
            {
                await SyncAllAsync();
            }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    public async Task<bool> SyncAllAsync()
    {
        if (IsSyncing || !IsOnline)
            return false;

        IsSyncing = true;
        var totalSteps = 4;
        var currentStep = 0;

        try
        {
            // Step 1: Sync user profile
            currentStep++;
            SyncProgress?.Invoke(this, new SyncProgressEventArgs("Kullanıcı profili senkronize ediliyor...", currentStep, totalSteps));
            await SyncUserProfileAsync();

            // Step 2: Upload pending voice recordings
            currentStep++;
            SyncProgress?.Invoke(this, new SyncProgressEventArgs("Ses kayıtları yükleniyor...", currentStep, totalSteps));
            await UploadPendingVoiceRecordingsAsync();

            // Step 3: Sync todos to server
            currentStep++;
            SyncProgress?.Invoke(this, new SyncProgressEventArgs("Görevler senkronize ediliyor...", currentStep, totalSteps));
            await SyncTodosToServerAsync();

            // Step 4: Download new todos from server
            currentStep++;
            SyncProgress?.Invoke(this, new SyncProgressEventArgs("Yeni görevler indiriliyor...", currentStep, totalSteps));
            await SyncTodosFromServerAsync();

            LastSyncTime = DateTime.Now;
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(true, "Senkronizasyon başarıyla tamamlandı"));
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
            SyncError?.Invoke(this, ex);
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(false, $"Senkronizasyon hatası: {ex.Message}"));
            return false;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private async Task SyncUserProfileAsync()
    {
        try
        {
            var user = _supabaseService.GetCurrentUser();
            if (user == null) return;

            var localProfile = await _todoStore.GetUserProfileAsync(user.Id);
            var serverProfile = await _supabaseService.GetOrCreateProfileAsync(
                localProfile?.FullName, 
                localProfile != null ? new Dictionary<string, object> { ["preferences"] = localProfile.PreferencesJson } : null
            );

            if (serverProfile != null && localProfile == null)
            {
                // Save server profile to local
                var newLocalProfile = new LocalUserProfile
                {
                    Id = serverProfile.Id,
                    Email = serverProfile.Email,
                    FullName = serverProfile.FullName,
                    AvatarUrl = serverProfile.AvatarUrl,
                    PreferencesJson = System.Text.Json.JsonSerializer.Serialize(serverProfile.Preferences),
                    NeedsSync = false
                };
                await _todoStore.SaveUserProfileAsync(newLocalProfile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"User profile sync failed: {ex.Message}");
        }
    }

    private async Task UploadPendingVoiceRecordingsAsync()
    {
        try
        {
            var pendingRecordings = await _todoStore.GetVoiceRecordingsAsync();
            var needsUpload = pendingRecordings.Where(r => r.NeedsSync && !string.IsNullOrEmpty(r.LocalFilePath) && File.Exists(r.LocalFilePath)).ToList();

            foreach (var recording in needsUpload)
            {
                try
                {
                    var audioData = await _audioService.GetRecordingDataAsync(recording.LocalFilePath);
                    if (audioData != null)
                    {
                        var base64Data = _audioService.ConvertToBase64(audioData);
                        var uploadedRecording = await _supabaseService.UploadVoiceRecordingAsync(
                            base64Data, recording.FileName, recording.TodoId, recording.Duration);

                        if (uploadedRecording != null)
                        {
                            // Update local record with server URL
                            recording.FileUrl = uploadedRecording.FileUrl;
                            recording.NeedsSync = false;
                            await _todoStore.SaveVoiceRecordingAsync(recording);

                            await _todoStore.UpdateSyncStatusAsync(recording.Id, "VoiceRecording", true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Voice recording upload failed for {recording.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Voice recordings sync failed: {ex.Message}");
        }
    }

    private async Task SyncTodosToServerAsync()
    {
        try
        {
            var pendingTodos = await _todoStore.GetPendingTodosAsync();

            foreach (var localTodo in pendingTodos)
            {
                try
                {
                    // Tombstone: push the delete to the server, then remove locally
                    if (localTodo.IsDeleted)
                    {
                        var deleted = await _supabaseService.DeleteTodoAsync(localTodo.Id);
                        if (deleted)
                        {
                            await _todoStore.DeleteTodoAsync(localTodo.Id);
                            await _todoStore.UpdateSyncStatusAsync(localTodo.Id, "Todo", true);
                        }
                        continue;
                    }

                    TodoDto? serverTodo = null;

                    if (await _todoStore.GetSyncStatusAsync(localTodo.Id) == null)
                    {
                        // Create new todo on server
                        serverTodo = await _supabaseService.CreateTodoAsync(
                            localTodo.Title,
                            localTodo.Description,
                            localTodo.Priority,
                            localTodo.DueDate,
                            localTodo.VoiceRecordingUrl,
                            localTodo.VoiceDuration
                        );
                    }
                    else
                    {
                        // Update existing todo on server
                        serverTodo = await _supabaseService.UpdateTodoAsync(localTodo.Id, new
                        {
                            title = localTodo.Title,
                            description = localTodo.Description,
                            completed = localTodo.Completed,
                            priority = localTodo.Priority,
                            dueDate = localTodo.DueDate?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            voice_recording_url = localTodo.VoiceRecordingUrl,
                            voice_duration = localTodo.VoiceDuration
                        });
                    }

                    if (serverTodo != null)
                    {
                        // Update local record
                        localTodo.NeedsSync = false;
                        await _todoStore.SaveTodoAsync(localTodo);
                        await _todoStore.UpdateSyncStatusAsync(localTodo.Id, "Todo", true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Todo sync to server failed for {localTodo.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Todos sync to server failed: {ex.Message}");
        }
    }

    private async Task SyncTodosFromServerAsync()
    {
        try
        {
            var serverTodos = await _supabaseService.GetTodosAsync();

            foreach (var serverTodo in serverTodos)
            {
                var localTodo = await _todoStore.GetTodoAsync(serverTodo.Id);
                
                if (localTodo == null)
                {
                    // New todo from server, add to local
                    var newLocalTodo = LocalTodo.FromTodo(serverTodo.ToTodo(), false);
                    await _todoStore.SaveTodoAsync(newLocalTodo);
                    await _todoStore.UpdateSyncStatusAsync(serverTodo.Id, "Todo", true);
                }
                else if (!localTodo.NeedsSync && !localTodo.IsDeleted && serverTodo.UpdatedAt > localTodo.UpdatedAt)
                {
                    // Server version is newer and local doesn't have pending changes
                    var updatedLocalTodo = LocalTodo.FromTodo(serverTodo.ToTodo(), false);
                    await _todoStore.SaveTodoAsync(updatedLocalTodo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Todos sync from server failed: {ex.Message}");
        }
    }

    public async Task<bool> CreateTodoAsync(string title, string? description = null, string priority = "medium", DateTime? dueDate = null, DateTime? reminderAt = null)
    {
        try
        {
            var user = _supabaseService.GetCurrentUser();
            var userId = user?.Id ?? "local-user";

            var localTodo = new LocalTodo
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Title = title,
                Description = description,
                Priority = priority,
                DueDate = dueDate,
                ReminderAt = reminderAt,
                NeedsSync = true
            };

            // Save locally first
            await _todoStore.SaveTodoAsync(localTodo);

            // Try to sync immediately if online and logged in
            if (IsOnline && user != null)
            {
                try
                {
                    var serverTodo = await _supabaseService.CreateTodoAsync(title, description, priority, dueDate, null, null, reminderAt);
                    if (serverTodo != null)
                    {
                        localTodo.NeedsSync = false;
                        await _todoStore.SaveTodoAsync(localTodo);
                        await _todoStore.UpdateSyncStatusAsync(localTodo.Id, "Todo", true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Immediate sync failed: {ex.Message}");
                    // Continue with local save
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Create todo failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateTodoAsync(string id, object updates)
    {
        try
        {
            var localTodo = await _todoStore.GetTodoAsync(id);
            if (localTodo == null) return false;

            // Apply updates to local todo
            var updateDict = updates.GetType().GetProperties()
                .ToDictionary(p => p.Name, p => p.GetValue(updates));

            foreach (var kvp in updateDict)
            {
                var prop = typeof(LocalTodo).GetProperty(kvp.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    prop.SetValue(localTodo, kvp.Value);
                }
            }

            localTodo.NeedsSync = true;
            localTodo.UpdatedAt = DateTime.UtcNow;
            
            // Save locally first
            await _todoStore.SaveTodoAsync(localTodo);

            // Try to sync immediately if online
            if (IsOnline)
            {
                try
                {
                    var serverTodo = await _supabaseService.UpdateTodoAsync(id, updates);
                    if (serverTodo != null)
                    {
                        localTodo.NeedsSync = false;
                        await _todoStore.SaveTodoAsync(localTodo);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Immediate update sync failed: {ex.Message}");
                    // Continue with local update
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update todo failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteTodoAsync(string id)
    {
        try
        {
            var localTodo = await _todoStore.GetTodoAsync(id);
            if (localTodo == null) return false;

            // Mark as tombstone locally first (ADR-010: delete must reach server,
            // offline deletes are never lost). UI hides tombstones via GetTodosAsync.
            localTodo.IsDeleted = true;
            localTodo.NeedsSync = true;
            localTodo.UpdatedAt = DateTime.UtcNow;
            await _todoStore.SaveTodoAsync(localTodo);

            // Try to delete from server if online (best-effort; periodic sync retries)
            if (IsOnline)
            {
                try
                {
                    var deleted = await _supabaseService.DeleteTodoAsync(id);
                    if (deleted)
                    {
                        // Server confirmed: purge the tombstone locally (no re-delete on next sync)
                        await _todoStore.DeleteTodoAsync(id);
                        await _todoStore.UpdateSyncStatusAsync(id, "Todo", true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Server delete failed: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Delete todo failed: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Todo>> GetTodosAsync()
    {
        try
        {
            var localTodos = await _todoStore.GetTodosAsync();
            return localTodos.Select(t => t.ToTodo()).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get todos failed: {ex.Message}");
            return new List<Todo>();
        }
    }

    // ---- Remote facade (ADR-012: Application must not see SupabaseService) ----

    public async Task InitializeAsync() => await _supabaseService.InitializeAsync();

    public Task<bool> IsUserLoggedInAsync() => _supabaseService.IsUserLoggedInAsync();

    public Task<bool> SignInAsync(string email, string password) => _supabaseService.SignInAsync(email, password);

    public Task<bool> SignUpAsync(string email, string password) => _supabaseService.SignUpAsync(email, password);

    public Task<bool> SignOutAsync() => _supabaseService.SignOutAsync();

    public Supabase.Gotrue.User? GetCurrentUser() => _supabaseService.GetCurrentUser();

    public Task<UserProfile?> GetOrCreateProfileAsync(string? fullName = null, Dictionary<string, object>? preferences = null)
        => _supabaseService.GetOrCreateProfileAsync(fullName, preferences);

    public Task<UserProfile?> UpdateProfileAsync(object updates) => _supabaseService.UpdateProfileAsync(updates);

    public Task<UserStats?> GetUserStatsAsync() => _supabaseService.GetUserStatsAsync();

    public Task<VoiceRecording?> UploadVoiceRecordingAsync(string audioData, string fileName, string? todoId = null, int? duration = null)
        => _supabaseService.UploadVoiceRecordingAsync(audioData, fileName, todoId, duration);

    public Task<(TodoDto?, List<VoiceRecording>)> GetTodoWithVoiceAsync(string todoId)
        => _supabaseService.GetTodoWithVoiceAsync(todoId);

    public void Dispose()
    {
        _syncTimer?.Dispose();
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class SyncProgressEventArgs : EventArgs
{
    public string Message { get; }
    public int CurrentStep { get; }
    public int TotalSteps { get; }
    public double Progress => TotalSteps > 0 ? (double)CurrentStep / TotalSteps : 0;

    public SyncProgressEventArgs(string message, int currentStep, int totalSteps)
    {
        Message = message;
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
    }
}

public class SyncCompletedEventArgs : EventArgs
{
    public bool Success { get; }
    public string Message { get; }

    public SyncCompletedEventArgs(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}