namespace TodoVoiceMaui.Core.Domain.Entities;

public class Todo
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Completed { get; set; }
    public string? VoiceRecordingUrl { get; set; }
    public int? VoiceDuration { get; set; }
    public string Priority { get; set; } = "medium";
    public DateTime? DueDate { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool HasVoiceRecording => !string.IsNullOrEmpty(VoiceRecordingUrl);
}
