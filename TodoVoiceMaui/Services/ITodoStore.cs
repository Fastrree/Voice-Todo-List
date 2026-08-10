using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Local todo persistence seam (ADR-012: ITodoStore — SQLite → in-memory test).
/// Application layer consumes this, never <see cref="SupabaseService"/> directly.
/// </summary>
public interface ITodoStore
{
    Task InitAsync();

    Task<List<LocalTodo>> GetTodosAsync();
    Task<LocalTodo?> GetTodoAsync(string id);
    Task<bool> SaveTodoAsync(LocalTodo todo);
    Task<bool> DeleteTodoAsync(string id);
    Task<List<LocalTodo>> GetPendingTodosAsync();

    Task<List<LocalVoiceRecording>> GetVoiceRecordingsAsync(string? todoId = null);
    Task<bool> SaveVoiceRecordingAsync(LocalVoiceRecording recording);
    Task<bool> DeleteVoiceRecordingAsync(string id);

    Task<LocalUserProfile?> GetUserProfileAsync(string userId);
    Task<bool> SaveUserProfileAsync(LocalUserProfile profile);

    Task<SyncStatus?> GetSyncStatusAsync(string entityId);
    Task<bool> UpdateSyncStatusAsync(string entityId, string entityType, bool synced, DateTime? lastSyncAt = null);
    Task<List<SyncStatus>> GetPendingSyncItemsAsync();

    Task ClearAllDataAsync();
}
