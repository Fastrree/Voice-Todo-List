using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace TodoVoiceMaui.Models;

public class UserProfile : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _email = string.Empty;
    private string? _fullName;
    private string? _avatarUrl;
    private UserPreferences _preferences = new();
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    [JsonProperty("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonProperty("email")]
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    [JsonProperty("full_name")]
    public string? FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    [JsonProperty("avatar_url")]
    public string? AvatarUrl
    {
        get => _avatarUrl;
        set => SetProperty(ref _avatarUrl, value);
    }

    [JsonProperty("preferences")]
    public UserPreferences Preferences
    {
        get => _preferences;
        set => SetProperty(ref _preferences, value);
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
    public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Email.Split('@')[0];

    public string Initials
    {
        get
        {
            if (!string.IsNullOrEmpty(FullName))
            {
                var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                return parts[0][0].ToString().ToUpper();
            }
            return Email[0].ToString().ToUpper();
        }
    }

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

public class UserPreferences : INotifyPropertyChanged
{
    private string _language = "tr";
    private string _theme = "light";
    private bool _enableNotifications = true;
    private bool _enableVoiceRecording = true;
    private bool _autoSync = true;
    private string _defaultPriority = "medium";

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    public bool EnableVoiceRecording
    {
        get => _enableVoiceRecording;
        set => SetProperty(ref _enableVoiceRecording, value);
    }

    public bool AutoSync
    {
        get => _autoSync;
        set => SetProperty(ref _autoSync, value);
    }

    public string DefaultPriority
    {
        get => _defaultPriority;
        set => SetProperty(ref _defaultPriority, value);
    }

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