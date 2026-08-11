using System.ComponentModel;
using System.Runtime.CompilerServices;
using TodoVoiceMaui.Core.Domain.Entities;

namespace TodoVoiceMaui.Models;

public class TodoListItem : INotifyPropertyChanged
{
    public Todo Model { get; }

    public TodoListItem(Todo model)
    {
        Model = model;
    }

    public string Id => Model.Id;

    public string Title => Model.Title;

    public string? Description => Model.Description;

    public bool Completed
    {
        get => Model.Completed;
        set
        {
            if (Model.Completed == value) return;
            Model.Completed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
        }
    }

    public string? VoiceRecordingUrl => Model.VoiceRecordingUrl;

    public int? VoiceDuration => Model.VoiceDuration;

    public string Priority => Model.Priority;

    public DateTime? DueDate => Model.DueDate;

    public DateTime? ReminderAt => Model.ReminderAt;

    public DateTime CreatedAt => Model.CreatedAt;

    public DateTime UpdatedAt => Model.UpdatedAt;

    public bool HasVoiceRecording => Model.HasVoiceRecording;

    public bool HasReminder => ReminderAt.HasValue;

    public string ReminderLabel => ReminderAt.HasValue
        ? ReminderAt.Value.ToString("dd MMM · HH:mm", new System.Globalization.CultureInfo("tr-TR"))
        : string.Empty;

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
}
