namespace TodoVoiceMaui.Core.Application.Voice;

public enum TranscriptionConfidence
{
    Unknown = 0,
    Low,
    Medium,
    High
}

public sealed class TranscriptionResult
{
    public string Text { get; }
    public TranscriptionConfidence Confidence { get; }
    public IReadOnlyList<string> Alternates { get; }
    public string Provider { get; }

    public TranscriptionResult(
        string text,
        TranscriptionConfidence confidence,
        IReadOnlyList<string>? alternates = null,
        string provider = "unknown")
    {
        Text = text;
        Confidence = confidence;
        Alternates = alternates ?? Array.Empty<string>();
        Provider = provider;
    }
}
