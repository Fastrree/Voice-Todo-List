using System;
using System.Threading;
using System.Threading.Tasks;
using TodoVoiceMaui.Core.Application.Todos;

namespace TodoVoiceMaui.Services;

public class TodoCommandSink : ITodoCommandSink
{
    private readonly SyncService _syncService;

    public TodoCommandSink(SyncService syncService)
    {
        _syncService = syncService;
    }

    public Task<bool> CreateTodoAsync(string title, CancellationToken ct = default)
        => _syncService.CreateTodoAsync(title);

    public Task<bool> CompleteTodoAsync(string todoId, CancellationToken ct = default)
        => _syncService.UpdateTodoAsync(todoId, new { completed = true });

    public Task<bool> SetReminderAsync(string todoId, DateTime reminderAt, CancellationToken ct = default)
        => _syncService.UpdateTodoAsync(todoId, new { reminderAt = reminderAt.ToString("yyyy-MM-ddTHH:mm:ssZ") });
}
