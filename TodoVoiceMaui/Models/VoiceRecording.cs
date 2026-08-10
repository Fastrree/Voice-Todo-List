using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace TodoVoiceMaui.Models;

public class VoiceRecording : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _todoId = string.Empty;
    private string _userId = string.Empty;
    private string _fileUrl = string.Empty;
    private string _fileName = string.Empty;
    private int _fileSize;
    private int? _duration;
    private string _mimeType = "audio/wav";
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isPlaying;
    private double _playbackProgress;

    [JsonProperty("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonProperty("todo_id")]
    public string TodoId
    {
        get => _todoId;
        set => SetProperty(ref _todoId, value);
    }

    [JsonProperty("user_id")]
    public string UserId
    {
        get => _userId;
        set => SetProperty(ref _userId, value);
    }

    [JsonProperty("file_url")]
    public string FileUrl
    {
        get => _fileUrl;
        set => SetProperty(ref _fileUrl, value);
    }

    [JsonProperty("file_name")]
    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    [JsonProperty("file_size")]
    public int FileSize
    {
        get => _fileSize;
        set => SetProperty(ref _fileSize, value);
    }

    [JsonProperty("duration")]
    public int? Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    [JsonProperty("mime_type")]
    public string MimeType
    {
        get => _mimeType;
        set => SetProperty(ref _mimeType, value);
    }

    [JsonProperty("created_at")]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    // Computed properties
    public string FormattedDuration => Duration.HasValue && Duration > 0
        ? TimeSpan.FromSeconds(Duration.Value).ToString(@"mm\:ss")
        : "00:00";

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayButtonText));
            }
        }
    }

    public double PlaybackProgress
    {
        get => _playbackProgress;
        set => SetProperty(ref _playbackProgress, value);
    }

    public string PlayButtonText => IsPlaying ? "⏸️" : "▶️";

    public string FormattedFileSize => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSize / (1024 * 1024):F1} MB",
        _ => $"{FileSize / (1024 * 1024 * 1024):F1} GB"
    };

    public bool IsPlayable => !string.IsNullOrEmpty(FileUrl) && 
                             (MimeType.Contains("audio") || MimeType.Contains("wav") || MimeType.Contains("mp3"));

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