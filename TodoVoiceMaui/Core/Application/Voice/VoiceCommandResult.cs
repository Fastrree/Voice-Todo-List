namespace TodoVoiceMaui.Core.Application.Voice;

public sealed class VoiceCommandResult
{
    public bool Success { get; }
    public string? Message { get; }

    public VoiceCommandResult(bool success, string? message = null)
    {
        Success = success;
        Message = message;
    }

    public static VoiceCommandResult Ok(string? message = null)
        => new(true, message);

    public static VoiceCommandResult Fail(string? message = null)
        => new(false, message);
}
