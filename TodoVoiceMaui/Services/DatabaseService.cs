using SQLite;
using TodoVoiceMaui.Models;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.Services;

public class DatabaseService : ITodoStore
{
    private SQLiteAsyncConnection? _database;

    public async Task InitAsync()
    {
        if (_database is not null)
            return;

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "TodoVoice.db");
        _database = new SQLiteAsyncConnection(databasePath);

        // Create tables
        await _database.CreateTableAsync<LocalTodo>();
        await _database.CreateTableAsync<LocalVoiceRecording>();
        await _database.CreateTableAsync<LocalUserProfile>();
        await _database.CreateTableAsync<SyncStatus>();

        await MigrateAsync();
    }

    private async Task MigrateAsync()
    {
        try
        {
            var columns = await Database.QueryAsync<SqliteColumnInfo>(
                "PRAGMA table_info(Todos)");
            if (columns.All(c => c.Name != "ReminderAt"))
            {
                await Database.ExecuteAsync("ALTER TABLE Todos ADD COLUMN ReminderAt datetime");
            }
            if (columns.All(c => c.Name != "IsDeleted"))
            {
                await Database.ExecuteAsync("ALTER TABLE Todos ADD COLUMN IsDeleted integer NOT NULL DEFAULT 0");
            }
            if (columns.All(c => c.Name != "LocalVersion"))
            {
                await Database.ExecuteAsync("ALTER TABLE Todos ADD COLUMN LocalVersion integer NOT NULL DEFAULT 0");
            }
        }
        catch
        {
            // Migration is best-effort; ignore failures
        }
    }

    private class SqliteColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private SQLiteAsyncConnection Database => 
        _database ?? throw new InvalidOperationException("Database not initialized");

    // Todo operations
    public async Task<List<LocalTodo>> GetTodosAsync()
    {
        await InitAsync();
        return await Database.Table<LocalTodo>()
                            .Where(t => !t.IsDeleted)
                            .OrderByDescending(t => t.CreatedAt)
                            .ToListAsync();
    }

    public async Task<LocalTodo?> GetTodoAsync(string id)
    {
        await InitAsync();
        return await Database.Table<LocalTodo>()
                            .Where(t => t.Id == id)
                            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveTodoAsync(LocalTodo todo)
    {
        await InitAsync();
        
        if (string.IsNullOrEmpty(todo.Id))
            todo.Id = Guid.NewGuid().ToString();
        
        var existing = await GetTodoAsync(todo.Id);
        if (existing != null)
        {
            todo.CreatedAt = existing.CreatedAt;
            todo.LocalVersion = existing.LocalVersion + 1;
            todo.UpdatedAt = DateTime.UtcNow;
            return await Database.UpdateAsync(todo) > 0;
        }
        else
        {
            todo.LocalVersion = 1;
            todo.CreatedAt = DateTime.UtcNow;
            todo.UpdatedAt = DateTime.UtcNow;
            return await Database.InsertAsync(todo) > 0;
        }
    }

    public async Task<bool> DeleteTodoAsync(string id)
    {
        await InitAsync();
        
        // Also delete related voice recordings
        await Database.Table<LocalVoiceRecording>()
                      .Where(v => v.TodoId == id)
                      .DeleteAsync();
        
        return await Database.Table<LocalTodo>()
                             .Where(t => t.Id == id)
                             .DeleteAsync() > 0;
    }

    public async Task<List<LocalTodo>> GetPendingTodosAsync()
    {
        await InitAsync();
        return await Database.Table<LocalTodo>()
                            .Where(t => t.NeedsSync)
                            .ToListAsync();
    }

    // Voice recording operations
    public async Task<List<LocalVoiceRecording>> GetVoiceRecordingsAsync(string? todoId = null)
    {
        await InitAsync();
        
        var query = Database.Table<LocalVoiceRecording>();
        if (!string.IsNullOrEmpty(todoId))
            query = query.Where(v => v.TodoId == todoId);
        
        return await query.OrderByDescending(v => v.CreatedAt)
                         .ToListAsync();
    }

    public async Task<bool> SaveVoiceRecordingAsync(LocalVoiceRecording recording)
    {
        await InitAsync();
        
        if (string.IsNullOrEmpty(recording.Id))
            recording.Id = Guid.NewGuid().ToString();
        
        var existing = await Database.Table<LocalVoiceRecording>()
                                   .Where(v => v.Id == recording.Id)
                                   .FirstOrDefaultAsync();
        
        if (existing != null)
        {
            return await Database.UpdateAsync(recording) > 0;
        }
        else
        {
            recording.CreatedAt = DateTime.UtcNow;
            return await Database.InsertAsync(recording) > 0;
        }
    }

    public async Task<bool> DeleteVoiceRecordingAsync(string id)
    {
        await InitAsync();
        return await Database.Table<LocalVoiceRecording>()
                             .Where(v => v.Id == id)
                             .DeleteAsync() > 0;
    }

    // User profile operations
    public async Task<LocalUserProfile?> GetUserProfileAsync(string userId)
    {
        await InitAsync();
        return await Database.Table<LocalUserProfile>()
                            .Where(p => p.Id == userId)
                            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveUserProfileAsync(LocalUserProfile profile)
    {
        await InitAsync();
        
        profile.UpdatedAt = DateTime.UtcNow;
        
        var existing = await GetUserProfileAsync(profile.Id);
        if (existing != null)
        {
            return await Database.UpdateAsync(profile) > 0;
        }
        else
        {
            profile.CreatedAt = DateTime.UtcNow;
            return await Database.InsertAsync(profile) > 0;
        }
    }

    // Sync status operations
    public async Task<SyncStatus?> GetSyncStatusAsync(string entityId)
    {
        await InitAsync();
        return await Database.Table<SyncStatus>()
                            .Where(s => s.EntityId == entityId)
                            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateSyncStatusAsync(string entityId, string entityType, bool synced, DateTime? lastSyncAt = null)
    {
        await InitAsync();
        
        var syncStatus = await GetSyncStatusAsync(entityId) ?? new SyncStatus
        {
            EntityId = entityId,
            EntityType = entityType
        };
        
        syncStatus.IsSynced = synced;
        syncStatus.LastSyncAt = lastSyncAt ?? DateTime.UtcNow;
        
        if (await GetSyncStatusAsync(entityId) != null)
        {
            return await Database.UpdateAsync(syncStatus) > 0;
        }
        else
        {
            return await Database.InsertAsync(syncStatus) > 0;
        }
    }

    public async Task<List<SyncStatus>> GetPendingSyncItemsAsync()
    {
        await InitAsync();
        return await Database.Table<SyncStatus>()
                            .Where(s => !s.IsSynced)
                            .ToListAsync();
    }

    // Clear all data (for logout)
    public async Task ClearAllDataAsync()
    {
        await InitAsync();
        
        await Database.DeleteAllAsync<LocalTodo>();
        await Database.DeleteAllAsync<LocalVoiceRecording>();
        await Database.DeleteAllAsync<LocalUserProfile>();
        await Database.DeleteAllAsync<SyncStatus>();
    }
}

// Local database models
[Table("Todos")]
public class LocalTodo
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Completed { get; set; }
    public string? VoiceRecordingUrl { get; set; }
    public int? VoiceDuration { get; set; }
    public string Priority { get; set; } = "medium";
    public DateTime? DueDate { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool NeedsSync { get; set; } = true;
    public bool IsDeleted { get; set; }
    public int LocalVersion { get; set; }
    public string? LocalVoiceFilePath { get; set; }

    // Convert to API model
    public Todo ToTodo()
    {
        return new Todo
        {
            Id = Id,
            UserId = UserId,
            Title = Title,
            Description = Description,
            Completed = Completed,
            VoiceRecordingUrl = VoiceRecordingUrl,
            VoiceDuration = VoiceDuration,
            Priority = Priority,
            DueDate = DueDate,
            ReminderAt = ReminderAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    // Create from API model
    public static LocalTodo FromTodo(Todo todo, bool needsSync = false)
    {
        return new LocalTodo
        {
            Id = todo.Id,
            UserId = todo.UserId,
            Title = todo.Title,
            Description = todo.Description,
            Completed = todo.Completed,
            VoiceRecordingUrl = todo.VoiceRecordingUrl,
            VoiceDuration = todo.VoiceDuration,
            Priority = todo.Priority,
            DueDate = todo.DueDate,
            ReminderAt = todo.ReminderAt,
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt,
            NeedsSync = needsSync,
            LocalVersion = 1
        };
    }
}

[Table("VoiceRecordings")]
public class LocalVoiceRecording
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string TodoId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public int? Duration { get; set; }
    public string MimeType { get; set; } = "audio/wav";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool NeedsSync { get; set; } = true;

    // Convert to API model
    public VoiceRecording ToVoiceRecording()
    {
        return new VoiceRecording
        {
            Id = Id,
            TodoId = TodoId,
            UserId = UserId,
            FileUrl = FileUrl ?? LocalFilePath,
            FileName = FileName,
            FileSize = FileSize,
            Duration = Duration,
            MimeType = MimeType,
            CreatedAt = CreatedAt
        };
    }
}

[Table("UserProfiles")]
public class LocalUserProfile
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferencesJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool NeedsSync { get; set; } = true;
}

[Table("SyncStatus")]
public class SyncStatus
{
    [PrimaryKey]
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public bool IsSynced { get; set; }
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
}