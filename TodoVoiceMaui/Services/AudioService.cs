using Plugin.Maui.Audio;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TodoVoiceMaui.Services;

public class AudioService : INotifyPropertyChanged
{
    private readonly IAudioManager _audioManager;
    private IAudioRecorder? _audioRecorder;
    private IAudioPlayer? _audioPlayer;
    private bool _isRecording;
    private bool _isPlaying;
    private TimeSpan _recordingDuration;
    private TimeSpan _playbackPosition;
    private string? _lastRecordingPath;

    public AudioService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set => SetProperty(ref _isRecording, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public TimeSpan RecordingDuration
    {
        get => _recordingDuration;
        private set => SetProperty(ref _recordingDuration, value);
    }

    public string? LastRecordingPath
    {
        get => _lastRecordingPath;
        private set => SetProperty(ref _lastRecordingPath, value);
    }

    public TimeSpan PlaybackPosition
    {
        get => _playbackPosition;
        private set => SetProperty(ref _playbackPosition, value);
    }

    public TimeSpan PlaybackDuration
    {
        get
        {
            var seconds = _audioPlayer?.Duration;
            return seconds.HasValue && seconds > 0
                ? TimeSpan.FromSeconds(seconds.Value)
                : TimeSpan.Zero;
        }
    }

    public bool HasRecording => !string.IsNullOrEmpty(LastRecordingPath) && File.Exists(LastRecordingPath);

    public event EventHandler<string>? RecordingCompleted;
    public event EventHandler<TimeSpan>? RecordingProgressUpdated;
    public event EventHandler<Exception>? RecordingError;
    public event EventHandler? PlaybackCompleted;
    public event EventHandler<Exception>? PlaybackError;
    public event EventHandler<TimeSpan>? PlaybackPositionUpdated;

    public async Task<bool> StartRecordingAsync()
    {
        try
        {
            if (IsRecording || IsPlaying)
                return false;

            // Check and request microphone permission
            var permissionStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (permissionStatus != PermissionStatus.Granted)
            {
                throw new UnauthorizedAccessException("Mikrofon izni gerekli. Lütfen uygulamanın mikrofon kullanma iznini verin.");
            }

            _audioRecorder = _audioManager.CreateRecorder();

            // Generate unique filename
            var fileName = $"voice_recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            // Start recording
            await _audioRecorder.StartAsync(filePath);
            IsRecording = true;
            LastRecordingPath = filePath;
            RecordingDuration = TimeSpan.Zero;

            // Start duration tracking
            _ = TrackRecordingDurationAsync();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Recording start failed: {ex.Message}");
            RecordingError?.Invoke(this, ex);
            return false;
        }
    }

    public async Task<string?> StopRecordingAsync()
    {
        try
        {
            if (!IsRecording || _audioRecorder == null)
                return null;

            var audioSource = await _audioRecorder.StopAsync();
            IsRecording = false;

            var recordingPath = LastRecordingPath;
            
            if (audioSource != null && !string.IsNullOrEmpty(recordingPath) && File.Exists(recordingPath))
            {
                RecordingCompleted?.Invoke(this, recordingPath);
                return recordingPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Recording stop failed: {ex.Message}");
            RecordingError?.Invoke(this, ex);
            IsRecording = false;
            return null;
        }
    }

    public async Task<bool> PlayRecordingAsync(string filePath)
    {
        try
        {
            if (IsPlaying || IsRecording)
                return false;

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Ses dosyası bulunamadı");

            _audioPlayer?.Dispose();

            _audioPlayer = _audioManager.CreatePlayer(filePath);

            _audioPlayer.PlaybackEnded += (s, e) =>
            {
                IsPlaying = false;
                PlaybackPosition = TimeSpan.Zero;
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            };

            _audioPlayer.Play();
            IsPlaying = true;
            PlaybackPosition = TimeSpan.Zero;
            _ = TrackPlaybackPositionAsync();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback failed: {ex.Message}");
            PlaybackError?.Invoke(this, ex);
            return false;
        }
    }

    public async Task<bool> PlayRecordingFromUrlAsync(string url)
    {
        try
        {
            if (IsPlaying || IsRecording)
                return false;

            _audioPlayer?.Dispose();

            _audioPlayer = _audioManager.CreatePlayer(url);

            _audioPlayer.PlaybackEnded += (s, e) =>
            {
                IsPlaying = false;
                PlaybackPosition = TimeSpan.Zero;
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            };

            _audioPlayer.Play();
            IsPlaying = true;
            PlaybackPosition = TimeSpan.Zero;
            _ = TrackPlaybackPositionAsync();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"URL playback failed: {ex.Message}");
            PlaybackError?.Invoke(this, ex);
            return false;
        }
    }

    public void StopPlayback()
    {
        try
        {
            _audioPlayer?.Stop();
            IsPlaying = false;
            PlaybackPosition = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Stop playback failed: {ex.Message}");
        }
    }

    public void PausePlayback()
    {
        try
        {
            _audioPlayer?.Pause();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pause playback failed: {ex.Message}");
        }
    }

    public async Task<byte[]?> GetRecordingDataAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get recording data failed: {ex.Message}");
            return null;
        }
    }

    public string ConvertToBase64(byte[] audioData)
    {
        try
        {
            var base64 = Convert.ToBase64String(audioData);
            return $"data:audio/wav;base64,{base64}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Base64 conversion failed: {ex.Message}");
            throw;
        }
    }

    public void DeleteRecording(string? filePath = null)
    {
        try
        {
            var pathToDelete = filePath ?? LastRecordingPath;
            if (!string.IsNullOrEmpty(pathToDelete) && File.Exists(pathToDelete))
            {
                File.Delete(pathToDelete);
                if (pathToDelete == LastRecordingPath)
                {
                    LastRecordingPath = null;
                    RecordingDuration = TimeSpan.Zero;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Delete recording failed: {ex.Message}");
        }
    }

    private async Task TrackRecordingDurationAsync()
    {
        var startTime = DateTime.Now;
        
        while (IsRecording)
        {
            RecordingDuration = DateTime.Now - startTime;
            RecordingProgressUpdated?.Invoke(this, RecordingDuration);
            await Task.Delay(100); // Update every 100ms
        }
    }

    private async Task TrackPlaybackPositionAsync()
    {
        while (IsPlaying)
        {
            var seconds = _audioPlayer?.CurrentPosition;
            PlaybackPosition = seconds.HasValue
                ? TimeSpan.FromSeconds(seconds.Value)
                : TimeSpan.Zero;
            PlaybackPositionUpdated?.Invoke(this, PlaybackPosition);
            await Task.Delay(100); // Update every 100ms
        }
    }

    public void Dispose()
    {
        try
        {
            _audioPlayer?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dispose failed: {ex.Message}");
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