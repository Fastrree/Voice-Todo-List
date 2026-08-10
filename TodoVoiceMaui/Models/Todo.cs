using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace TodoVoiceMaui.Models;

public class Todo : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _userId = string.Empty;
    private string _title = string.Empty;
    private string? _description;
    private bool _completed;
    private string? _voiceRecordingUrl;
    private int? _voiceDuration;
    private string _priority = "medium";
    private DateTime? _dueDate;
    private DateTime? _reminderAt;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    [JsonProperty("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonProperty("user_id")]
    public string UserId
    {
        get => _userId;
        set => SetProperty(ref _userId, value);
    }

    [JsonProperty("title")]
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    [JsonProperty("description")]
    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    [JsonProperty("completed")]
    public bool Completed
    {
        get => _completed;
        set => SetProperty(ref _completed, value);
    }

    [JsonProperty("voice_recording_url")]
    public string? VoiceRecordingUrl
    {
        get => _voiceRecordingUrl;
        set => SetProperty(ref _voiceRecordingUrl, value);
    }

    [JsonProperty("voice_duration")]
    public int? VoiceDuration
    {
        get => _voiceDuration;
        set => SetProperty(ref _voiceDuration, value);
    }

    [JsonProperty("priority")]
    public string Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    [JsonProperty("due_date")]
    public DateTime? DueDate
    {
        get => _dueDate;
        set => SetProperty(ref _dueDate, value);
    }

    [JsonProperty("reminder_at")]
    public DateTime? ReminderAt
    {
        get => _reminderAt;
        set => SetProperty(ref _reminderAt, value);
    }

    [JsonProperty("created_at")]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    // Computed properties
    public bool HasVoiceRecording => !string.IsNullOrEmpty(VoiceRecordingUrl);

    public string PriorityIcon => Priority switch
    {
        "high" => "🔴",
        "medium" => "🟡",
        "low" => "🟢",
        _ => "⚪"
    };

    public string StatusIcon => Completed ? "✅" : "⏳";

    public string FormattedDuration => VoiceDuration.HasValue && VoiceDuration > 0
        ? TimeSpan.FromSeconds(VoiceDuration.Value).ToString(@"mm\:ss")
        : string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}