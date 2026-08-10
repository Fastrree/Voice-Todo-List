using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TodoVoiceMaui.Services;

public class SpeechToTextService : INotifyPropertyChanged
{
    private bool _isListening;
    private bool _isAvailable;
    private string _liveTranscript = string.Empty;
    private Windows.Media.SpeechRecognition.SpeechRecognizer? _recognizer;

    public SpeechToTextService()
    {
#if WINDOWS
        try
        {
            var test = new Windows.Media.SpeechRecognition.SpeechRecognizer();
            test.Dispose();
            _isAvailable = true;
        }
        catch
        {
            _isAvailable = false;
        }
#endif
    }

    public bool IsListening
    {
        get => _isListening;
        private set => SetProperty(ref _isListening, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    public string LiveTranscript
    {
        get => _liveTranscript;
        private set => SetProperty(ref _liveTranscript, value);
    }

    public event EventHandler<string>? TranscriptionCompleted;
    public event EventHandler<Exception>? SpeechError;

    public async Task<bool> StartListeningAsync()
    {
        if (IsListening || !IsAvailable) return false;

#if WINDOWS
        try
        {
            IsListening = true;
            LiveTranscript = string.Empty;

            var recognizer = new Windows.Media.SpeechRecognition.SpeechRecognizer();
            _recognizer = recognizer;

            recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            recognizer.ContinuousRecognitionSession.Completed += OnRecognitionCompleted;

            await recognizer.ContinuousRecognitionSession.StartAsync();
            return true;
        }
        catch (Exception ex)
        {
            IsListening = false;
            _recognizer = null;
            System.Diagnostics.Debug.WriteLine($"Speech start failed: {ex.Message}");
            SpeechError?.Invoke(this, ex);
            return false;
        }
#else
        return false;
#endif
    }

    public async Task StopListeningAsync()
    {
        if (!IsListening) return;

#if WINDOWS
        try
        {
            var recognizer = _recognizer;
            if (recognizer != null)
            {
                await recognizer.ContinuousRecognitionSession.StopAsync();
            }
        }
        catch
        {
            // best-effort stop
        }
#endif
        IsListening = false;
        CleanupRecognizer();
    }

    public void StopListening()
    {
        _ = StopListeningAsync();
    }

#if WINDOWS
    private void OnResultGenerated(Windows.Media.SpeechRecognition.SpeechContinuousRecognitionSession sender,
        Windows.Media.SpeechRecognition.SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        try
        {
            var result = args.Result;
            if (result.Status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success)
            {
                LiveTranscript = result.Text;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Speech result failed: {ex.Message}");
        }
    }

    private void OnRecognitionCompleted(Windows.Media.SpeechRecognition.SpeechContinuousRecognitionSession sender,
        Windows.Media.SpeechRecognition.SpeechContinuousRecognitionCompletedEventArgs args)
    {
        var finalText = LiveTranscript?.Trim() ?? string.Empty;

        IsListening = false;
        LiveTranscript = string.Empty;
        CleanupRecognizer();

        if (args.Status == Windows.Media.SpeechRecognition.SpeechRecognitionResultStatus.Success
            && !string.IsNullOrWhiteSpace(finalText))
        {
            TranscriptionCompleted?.Invoke(this, finalText);
        }
    }
#endif

    private void CleanupRecognizer()
    {
#if WINDOWS
        if (_recognizer != null)
        {
            try
            {
                _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
                _recognizer.ContinuousRecognitionSession.Completed -= OnRecognitionCompleted;
                _recognizer.Dispose();
            }
            catch
            {
                // best-effort
            }
            _recognizer = null;
        }
#endif
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
