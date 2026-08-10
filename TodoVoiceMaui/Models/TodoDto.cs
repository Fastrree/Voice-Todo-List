using Newtonsoft.Json;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.Models;

public class TodoDto
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("completed")]
    public bool Completed { get; set; }

    [JsonProperty("voice_recording_url")]
    public string? VoiceRecordingUrl { get; set; }

    [JsonProperty("voice_duration")]
    public int? VoiceDuration { get; set; }

    [JsonProperty("priority")]
    public string Priority { get; set; } = "medium";

    [JsonProperty("due_date")]
    public DateTime? DueDate { get; set; }

    [JsonProperty("reminder_at")]
    public DateTime? ReminderAt { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Todo ToTodo()
    {
        return new Todo
        {
            Id = Id,
            UserId = UserId,
            Title = Title,
            Description = Description,
            Completed = Completed,
            VoiceRecordingUrl = VoiceRecordingUrl,
            VoiceDuration = VoiceDuration,
            Priority = Priority,
            DueDate = DueDate,
            ReminderAt = ReminderAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public static TodoDto FromTodo(Todo todo)
    {
        return new TodoDto
        {
            Id = todo.Id,
            UserId = todo.UserId,
            Title = todo.Title,
            Description = todo.Description,
            Completed = todo.Completed,
            VoiceRecordingUrl = todo.VoiceRecordingUrl,
            VoiceDuration = todo.VoiceDuration,
            Priority = todo.Priority,
            DueDate = todo.DueDate,
            ReminderAt = todo.ReminderAt,
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt
        };
    }
}
