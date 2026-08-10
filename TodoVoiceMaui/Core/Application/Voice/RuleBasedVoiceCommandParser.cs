using System;

namespace TodoVoiceMaui.Core.Application.Voice;

public sealed class RuleBasedVoiceCommandParser : IVoiceCommandParser
{
    private static readonly string[] CompleteKeywords =
    {
        "tamamla", "tamamlandı", "tamamla", "bitir", "yaptım", "tamamlanmış", "tamam"
    };

    private static readonly string[] RemindKeywords =
    {
        "hatırlat", "hatırlatma", "reminder", "alarm kur"
    };

    public VoiceCommand Parse(TranscriptionResult transcription)
    {
        var text = transcription.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return VoiceCommand.Unknown(string.Empty);

        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, CompleteKeywords))
            return new VoiceCommand(VoiceIntent.CompleteTodo, text);

        if (ContainsAny(lower, RemindKeywords))
            return new VoiceCommand(VoiceIntent.SetReminder, text);

        return new VoiceCommand(VoiceIntent.CreateTodo, text);
    }

    private static bool ContainsAny(string value, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (value.Contains(keyword, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
