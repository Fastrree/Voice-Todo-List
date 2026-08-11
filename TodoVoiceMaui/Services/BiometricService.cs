namespace TodoVoiceMaui.Services;

/// <summary>
/// Windows Hello biyometrik doğrulama (parmak izi / yüz / PIN) — UserConsentVerifier.
///
/// DİKKAT — unpackaged uygulama gerçeği: WindowsPackageType=None (paket kimliği yok)
/// çalıştığımız için bazı Windows 10/11 kurulumlarında bu WinRT API doğrulama
/// kullanılamayabilir (CheckAvailabilityAsync → DeviceNotPresent veya çağrı hatası).
/// Bu yüzden her çağrı savunmacıdır: kullanılamıyorsa UI "Windows Hello kullanılamıyor"
/// durumunu dürüstçe gösterir ve API anahtarları yine de Windows Credential Manager'da
/// (OS şifreli) güvende kalır — güvenlik gerilemez.
///
/// Windows Hello "PIN" girişini de kapsar: RequestVerificationAsync, kullanıcı PIN'i
/// veya biyometriyle onaylarsa Verified döner.
/// </summary>
public static class BiometricService
{
    /// <summary>Windows Hello bu cihazda kullanılabilir mi? (kullanıcı kurulumu + API erişimi)</summary>
    public static async Task<bool> IsAvailableAsync()
    {
#if WINDOWS
        try
        {
            var availability = await Windows.Security.Credentials.UI.UserConsentVerifier
                .CheckAvailabilityAsync();
            return availability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false; // unpackaged kısıtı veya WinRT erişim hatası
        }
#else
        return await Task.FromResult(false);
#endif
    }

    /// <summary>
    /// Kullanıcıdan Windows Hello (biyometri veya PIN) doğrulaması ister.
    /// True dönerse kullanıcı onayladı. Kullanılamıyor/kullanıcı iptal etti → false.
    /// </summary>
    public static async Task<bool> VerifyAsync(string message)
    {
#if WINDOWS
        try
        {
            var result = await Windows.Security.Credentials.UI.UserConsentVerifier
                .RequestVerificationAsync(message);
            return result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified;
        }
        catch
        {
            return false;
        }
#else
        return await Task.FromResult(false);
#endif
    }
}
