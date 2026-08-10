using System.ComponentModel;
using System.Runtime.CompilerServices;
using TodoVoiceMaui.Models;

namespace TodoVoiceMaui.Services;

public class SyncService : INotifyPropertyChanged
{
    private readonly SupabaseService _supabaseService;
    private readonly DatabaseService _databaseService;
    private readonly AudioService _audioService;
    private bool _isSyncing;
    private bool _isOnline = true;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private Timer? _syncTimer;

    public SyncService(SupabaseService supabaseService, DatabaseService databaseService, AudioService audioService)
    {
        _supabaseService = supabaseService;
        _databaseService = databaseService;
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

            var localProfile = await _databaseService.GetUserProfileAsync(user.Id);
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
                await _databaseService.SaveUserProfileAsync(newLocalProfile);
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
            var pendingRecordings = await _databaseService.GetVoiceRecordingsAsync();
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
                            await _databaseService.SaveVoiceRecordingAsync(recording);

                            await _databaseService.UpdateSyncStatusAsync(recording.Id, "VoiceRecording", true);
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
            var pendingTodos = await _databaseService.GetPendingTodosAsync();

            foreach (var localTodo in pendingTodos)
            {
                try
                {
                    Todo? serverTodo = null;

                    if (await _databaseService.GetSyncStatusAsync(localTodo.Id) == null)
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
                        await _databaseService.SaveTodoAsync(localTodo);
                        await _databaseService.UpdateSyncStatusAsync(localTodo.Id, "Todo", true);
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
                var localTodo = await _databaseService.GetTodoAsync(serverTodo.Id);
                
                if (localTodo == null)
                {
                    // New todo from server, add to local
                    var newLocalTodo = LocalTodo.FromTodo(serverTodo, false);
                    await _databaseService.SaveTodoAsync(newLocalTodo);
                    await _databaseService.UpdateSyncStatusAsync(serverTodo.Id, "Todo", true);
                }
                else if (serverTodo.UpdatedAt > localTodo.UpdatedAt && !localTodo.NeedsSync)
                {
                    // Server version is newer and local doesn't have pending changes
                    var updatedLocalTodo = LocalTodo.FromTodo(serverTodo, false);
                    await _databaseService.SaveTodoAsync(updatedLocalTodo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Todos sync from server failed: {ex.Message}");
        }
    }

    public async Task<bool> CreateTodoAsync(string title, string? description = null, string priority = "medium", DateTime? dueDate = null)
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
                NeedsSync = false
            };

            // Save locally first
            await _databaseService.SaveTodoAsync(localTodo);

            // Try to sync immediately if online and logged in
            if (IsOnline && user != null)
            {
                try
                {
                    var serverTodo = await _supabaseService.CreateTodoAsync(title, description, priority, dueDate);
                    if (serverTodo != null)
                    {
                        localTodo.NeedsSync = false;
                        await _databaseService.SaveTodoAsync(localTodo);
                        await _databaseService.UpdateSyncStatusAsync(localTodo.Id, "Todo", true);
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
            var localTodo = await _databaseService.GetTodoAsync(id);
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
            await _databaseService.SaveTodoAsync(localTodo);

            // Try to sync immediately if online
            if (IsOnline)
            {
                try
                {
                    var serverTodo = await _supabaseService.UpdateTodoAsync(id, updates);
                    if (serverTodo != null)
                    {
                        localTodo.NeedsSync = false;
                        await _databaseService.SaveTodoAsync(localTodo);
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
            // Delete locally first
            await _databaseService.DeleteTodoAsync(id);

            // Try to delete from server if online
            if (IsOnline)
            {
                try
                {
                    await _supabaseService.DeleteTodoAsync(id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Server delete failed: {ex.Message}");
                    // Todo is already deleted locally
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
            var localTodos = await _databaseService.GetTodosAsync();
            return localTodos.Select(t => t.ToTodo()).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get todos failed: {ex.Message}");
            return new List<Todo>();
        }
    }

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