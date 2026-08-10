namespace TodoVoiceMaui.Core.Application.Voice;

public interface IVoiceCommandParser
{
    VoiceCommand Parse(TranscriptionResult transcription);
}
