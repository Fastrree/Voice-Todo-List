using System;
using System.Threading;
using System.Threading.Tasks;

namespace TodoVoiceMaui.Core.Application.Todos;

public interface ITodoCommandSink
{
    Task<bool> CreateTodoAsync(string title, DateTime? reminderAt = null, CancellationToken ct = default);
    Task<bool> CompleteTodoAsync(string todoId, CancellationToken ct = default);
    Task<bool> SetReminderAsync(string todoId, DateTime reminderAt, CancellationToken ct = default);
}
