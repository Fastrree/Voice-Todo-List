namespace TodoVoiceMaui.Core.Application.Voice;

public enum VoiceIntent
{
    CreateTodo = 0,
    CompleteTodo,
    SetReminder,
    UnknownIntent
}

public sealed class VoiceCommand
{
    public VoiceIntent Intent { get; }
    public string Transcript { get; }
    public string? TargetId { get; }

    public VoiceCommand(VoiceIntent intent, string transcript, string? targetId = null)
    {
        Intent = intent;
        Transcript = transcript;
        TargetId = targetId;
    }

    public static VoiceCommand Unknown(string transcript)
        => new(VoiceIntent.UnknownIntent, transcript);
}
