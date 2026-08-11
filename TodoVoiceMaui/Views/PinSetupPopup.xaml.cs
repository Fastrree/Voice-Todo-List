using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Services;

namespace TodoVoiceMaui.Views;

/// <summary>
/// Şifre ayarlayıcı modalı: PIN kur (ilk kez) veya değiştir (mevcut PIN doğrulamasıyla).
/// Sonuç <see cref="ResultTask"/> ile okunur — kullanıcı kaydetti mi?
/// </summary>
public partial class PinSetupPopup : Popup
{
    private readonly PinSetupViewModel _viewModel;

    public PinSetupPopup(bool isChanging)
    {
        InitializeComponent();
        _viewModel = new PinSetupViewModel(isChanging);
        BindingContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        // Dışa tıklama / kapama → iptal sayılır (bekleyen çağıranı asla asılı bırakma)
        Closed += (_, _) =>
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.Complete(false);
        };
    }

    /// <summary>Kullanıcı PIN kaydetti mi? (modal kapandıktan sonra await edilir)</summary>
    public Task<bool> ResultTask => _viewModel.ResultTask;

    private void OnRequestClose(bool _) => Close();
}

public partial class PinSetupViewModel : ObservableObject
{
    private readonly bool _isChanging;
    private readonly TaskCompletionSource<bool> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PinSetupViewModel(bool isChanging) => _isChanging = isChanging;

    public Task<bool> ResultTask => _tcs.Task;

    public bool IsChanging => _isChanging;

    public string TitleText => _isChanging ? "PIN DEĞİŞTİR" : "PIN OLUŞTUR";

    public string SubtitleText => _isChanging
        ? "Önce mevcut PIN'ini doğrula, sonra yenisini belirle."
        : "Ayarlar kilidi için 4-8 haneli bir PIN belirle.";

    [ObservableProperty]
    private string currentPin = string.Empty;

    [ObservableProperty]
    private string newPin = string.Empty;

    [ObservableProperty]
    private string confirmPin = string.Empty;

    [ObservableProperty]
    private bool isMasked = true;

    [ObservableProperty]
    private string errorText = string.Empty;

    [ObservableProperty]
    private bool hasError;

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
    private void Save()
    {
        ErrorText = string.Empty;
        HasError = false;

        var next = NewPin?.Trim() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(next, @"^\d{4,8}$"))
        {
            ShowError("PIN yalnızca rakamlardan oluşmalı ve 4-8 hane olmalı (örn. 1234).");
            return;
        }
        if (next != (ConfirmPin?.Trim() ?? string.Empty))
        {
            ShowError("PIN'ler eşleşmiyor — aynı PIN'i iki kez gir.");
            return;
        }

        if (_isChanging)
        {
            var current = CurrentPin?.Trim() ?? string.Empty;
            if (current.Length == 0)
            {
                ShowError("Mevcut PIN'i girin.");
                return;
            }
            // Mevcut PIN'i doğrular + yeniyi kurar (tek atomik çağrı)
            if (!AppLockService.ChangePin(current, next))
            {
                ShowError("Mevcut PIN hatalı — tekrar deneyin.");
                return;
            }
        }
        else
        {
            AppLockService.SetPin(next);
        }
        SoundEffectService.Play(SoundEffectService.SoundKind.Success);
        Complete(true);
        RequestClose?.Invoke(true);
    }

    private void ShowError(string message)
    {
        ErrorText = message;
        HasError = true;
    }
}
