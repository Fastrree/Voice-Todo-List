using System.Threading;
using System.Threading.Tasks;
using TodoVoiceMaui.Core.Application.Todos;

namespace TodoVoiceMaui.Core.Application.Voice;

public sealed class VoiceCommandHandler : IVoiceCommandHandler
{
    private readonly ITodoCommandSink _todoSink;

    public VoiceCommandHandler(ITodoCommandSink todoSink)
    {
        _todoSink = todoSink;
    }

    public async Task<VoiceCommandResult> HandleAsync(VoiceCommand command, CancellationToken ct = default)
    {
        switch (command.Intent)
        {
            case VoiceIntent.CreateTodo:
                return await CreateTodoAsync(command, ct);
            case VoiceIntent.CompleteTodo:
                return await CompleteTodoAsync(command, ct);
            case VoiceIntent.SetReminder:
                return await SetReminderAsync(command, ct);
            case VoiceIntent.UnknownIntent:
            default:
                return await CreateTodoAsync(command, ct);
        }
    }

    private async Task<VoiceCommandResult> CreateTodoAsync(VoiceCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Transcript))
            return VoiceCommandResult.Fail("Transkript boş");

        var ok = await _todoSink.CreateTodoAsync(command.Transcript, ct);
        return ok ? VoiceCommandResult.Ok("Görev oluşturuldu") : VoiceCommandResult.Fail("Görev oluşturulamadı");
    }

    private async Task<VoiceCommandResult> CompleteTodoAsync(VoiceCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.TargetId))
            return VoiceCommandResult.Fail("Hedef görev belirtilmemiş");

        var ok = await _todoSink.CompleteTodoAsync(command.TargetId, ct);
        return ok ? VoiceCommandResult.Ok("Görev tamamlandı") : VoiceCommandResult.Fail("Görev tamamlanamadı");
    }

    private async Task<VoiceCommandResult> SetReminderAsync(VoiceCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.TargetId))
            return VoiceCommandResult.Fail("Hedef görev belirtilmemiş");

        var reminderAt = ExtractReminderAt(command.Transcript);
        if (reminderAt == null)
            return VoiceCommandResult.Fail("Hatırlatma zamanı çözümlenemedi");

        var ok = await _todoSink.SetReminderAsync(command.TargetId, reminderAt.Value, ct);
        return ok ? VoiceCommandResult.Ok("Hatırlatıcı ayarlandı") : VoiceCommandResult.Fail("Hatırlatıcı ayarlanamadı");
    }

    private static System.DateTime? ExtractReminderAt(string transcript)
    {
        var text = transcript.ToLowerInvariant();

        if (text.Contains("yarın", System.StringComparison.Ordinal))
            return System.DateTime.Today.AddDays(1).AddHours(9);

        if (text.Contains("bugün", System.StringComparison.Ordinal))
            return System.DateTime.Today.AddHours(9);

        return null;
    }
}
