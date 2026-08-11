using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Services;

namespace TodoVoiceMaui.Views;

/// <summary>
/// PIN doğrulama modalı (örn. kilitli API anahtarını göstermek için).
/// Sonuç <see cref="ResultTask"/> ile okunur — kullanıcı doğruladı mı?
/// </summary>
public partial class PinVerifyPopup : Popup
{
    private readonly PinVerifyViewModel _viewModel;

    public PinVerifyPopup(string purpose)
    {
        InitializeComponent();
        _viewModel = new PinVerifyViewModel(purpose);
        BindingContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) =>
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.Complete(false);
        };
    }

    /// <summary>PIN doğrulandı mı? (modal kapandıktan sonra await edilir)</summary>
    public Task<bool> ResultTask => _viewModel.ResultTask;

    private void OnRequestClose(bool _) => Close();
}

public partial class PinVerifyViewModel : ObservableObject
{
    private readonly TaskCompletionSource<bool> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PinVerifyViewModel(string purpose) => PurposeText = purpose;

    public Task<bool> ResultTask => _tcs.Task;

    public string PurposeText { get; }

    [ObservableProperty]
    private string pin = string.Empty;

    [ObservableProperty]
    private string errorText = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private bool isMasked = true;

    public event Action<bool>? RequestClose;

    /// <summary>Dışa tıklama dahil her kapanışta sonucu tamamla (idempotent).</summary>
    public void Complete(bool ok) => _tcs.TrySetResult(ok);

    [RelayCommand]
    private void ToggleMask() => IsMasked = !IsMasked;

    [RelayCommand]
    private void Cancel()
    {
        Complete(false);
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    private void Verify()
    {
        var value = Pin?.Trim() ?? string.Empty;
        if (AppLockService.VerifyPin(value))
        {
            SoundEffectService.Play(SoundEffectService.SoundKind.Success);
            Complete(true);
            RequestClose?.Invoke(true);
        }
        else
        {
            Pin = string.Empty;
            ErrorText = "PIN hatalı — tekrar deneyin.";
            HasError = true;
        }
    }
}
