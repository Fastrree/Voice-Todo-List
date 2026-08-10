using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoVoiceMaui.Services;
using TodoVoiceMaui.Views;

namespace TodoVoiceMaui.ViewModels;

public partial class LoginPageViewModel : ObservableObject
{
    private readonly SyncService _syncService;
    private readonly ITodoStore _todoStore;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private bool isSignupMode = false;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError = false;

    public LoginPageViewModel(SyncService syncService, ITodoStore todoStore)
    {
        _syncService = syncService;
        _todoStore = todoStore;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsLoading) return;

        try
        {
            ClearError();
            IsLoading = true;

            if (!ValidateInput())
                return;

            var success = await _syncService.SignInAsync(Email, Password);
            
            if (success)
            {
                // Initialize local database
                await _todoStore.InitAsync();
                
                // Navigate to main app
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            }
            else
            {
                SetError("Giriş başarısız. E-posta ve şifrenizi kontrol edin.");
            }
        }
        catch (Exception ex)
        {
            SetError($"Giriş yapılamadı: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SignUpAsync()
    {
        if (IsLoading) return;

        try
        {
            ClearError();
            IsLoading = true;

            if (!ValidateSignupInput())
                return;

            var success = await _syncService.SignUpAsync(Email, Password);
            
            if (success)
            {
                SetError("Hesap oluşturuldu! E-posta adresinizi doğrulayın ve giriş yapın.", false);
                ToggleMode();
            }
            else
            {
                SetError("Hesap oluşturulamadı. Lütfen tekrar deneyin.");
            }
        }
        catch (Exception ex)
        {
            SetError($"Hesap oluşturulamadı: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsSignupMode = !IsSignupMode;
        ClearError();
        ClearForm();
    }

    [RelayCommand]
    private void ClearForm()
    {
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ClearError();
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            SetError("E-posta adresi gerekli.");
            return false;
        }

        if (!IsValidEmail(Email))
        {
            SetError("Geçerli bir e-posta adresi girin.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            SetError("Şifre gerekli.");
            return false;
        }

        if (Password.Length < 6)
        {
            SetError("Şifre en az 6 karakter olmalıdır.");
            return false;
        }

        return true;
    }

    private bool ValidateSignupInput()
    {
        if (!ValidateInput())
            return false;

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            SetError("Şifre onayı gerekli.");
            return false;
        }

        if (Password != ConfirmPassword)
        {
            SetError("Şifreler eşleşmiyor.");
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void SetError(string message, bool isError = true)
    {
        ErrorMessage = message;
        HasError = isError;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    // Computed properties
    public string PrimaryButtonText => IsSignupMode ? "Hesap Oluştur" : "Giriş Yap";
    public string SecondaryButtonText => IsSignupMode ? "Zaten hesabınız var mı? Giriş yapın" : "Hesabınız yok mu? Hesap oluşturun";
    public string TitleText => IsSignupMode ? "Hesap Oluştur" : "Giriş Yap";
    public string SubtitleText => IsSignupMode ? "Todo Voice uygulamasına hoş geldiniz" : "Hesabınıza giriş yapın";
}