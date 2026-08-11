namespace TodoVoiceMaui.Core.Application.Voice;

public enum VoiceIntent
{
    Create = 0,
    Complete,
    SetReminder,
    UnknownIntent
}

public sealed class VoiceCommand
{
    public VoiceIntent Intent { get; }
    public string Transcript { get; }
    public string? TargetId { get; }

    /// <summary>
    /// Komutla birlikte çözümlenen hatırlatma zamanı ("10 dakika sonra...").
    /// Parser doldurur; handler görevi bu zamanla oluşturur.
    /// </summary>
    public DateTime? ReminderAt { get; }

    public VoiceCommand(VoiceIntent intent, string transcript, string? targetId = null, DateTime? reminderAt = null)
    {
        Intent = intent;
        Transcript = transcript;
        TargetId = targetId;
        ReminderAt = reminderAt;
    }

    public static VoiceCommand Unknown(string transcript)
        => new(VoiceIntent.UnknownIntent, transcript);
}
