using System.Security.Cryptography;
using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>Uygulama kilidi yöntemi.</summary>
public enum AppLockMethod
{
    /// <summary>Kilit kapalı.</summary>
    None = 0,

    /// <summary>Kullanıcının kendi belirlediği PIN.</summary>
    Pin = 1,

    /// <summary>Windows Hello (parmak izi / yüz / Windows PIN) biyometrik doğrulama.</summary>
    WindowsHello = 2
}

/// <summary>
/// Uygulama kilidi (GÜVENLİK): PIN veya Windows Hello.
///
/// PIN, tuzlu SHA-256 özeti olarak Preferences'ta saklanır — düz metin asla
/// diskte tutulmaz. Oturum açma durumu statik tutulur: uygulama yeniden
/// başlayana kadar bir kez doğrulayan kullanıcı tekrar sorulmaz ("Ayarlar
/// sekmesine geçerken sor" açıksa tab değişimlerinde de).
/// </summary>
public static class AppLockService
{
    private const string MethodKey = "app_lock_method";
    private const string PinSaltKey = "app_lock_pin_salt";
    private const string PinHashKey = "app_lock_pin_hash";
    private const string AskSettingsKey = "app_lock_ask_settings";

    /// <summary>Aktif kilit yöntemi (kalıcı).</summary>
    public static AppLockMethod Method
    {
        get => (AppLockMethod)Preferences.Default.Get(MethodKey, (int)AppLockMethod.None);
        set => Preferences.Default.Set(MethodKey, (int)value);
    }

    /// <summary>Herhangi bir kilit aktif mi?</summary>
    public static bool IsAnyLockEnabled => Method != AppLockMethod.None;

    /// <summary>Ayarlar sekmesine geçerken kilit sorusu sorulsun mu? (kalıcı)</summary>
    public static bool AskOnSettingsEntry
    {
        get => Preferences.Default.Get(AskSettingsKey, true);
        set => Preferences.Default.Set(AskSettingsKey, value);
    }

    /// <summary>PIN kurulmuş mu?</summary>
    public static bool IsPinSet => !string.IsNullOrEmpty(Preferences.Default.Get(PinHashKey, string.Empty));

    private static bool _unlockedInSession;

    /// <summary>PIN kur: rastgele tuz üret + SHA-256 özeti sakla. Yöntem PIN'e geçer.</summary>
    public static void SetPin(string pin)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var saltHex = Convert.ToHexString(salt);
        Preferences.Default.Set(PinSaltKey, saltHex);
        Preferences.Default.Set(PinHashKey, HashPin(pin, saltHex));

        if (Method != AppLockMethod.Pin)
            Method = AppLockMethod.Pin;
    }

    /// <summary>Girilen PIN kurulu PIN ile eşleşiyor mu? (sabit zamanlı karşılaştırma)</summary>
    public static bool VerifyPin(string pin)
    {
        var salt = Preferences.Default.Get(PinSaltKey, string.Empty);
        var expected = Preferences.Default.Get(PinHashKey, string.Empty);
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expected))
            return false;

        var actual = HashPin(pin, salt);
        if (actual.Length != expected.Length)
            return false;

        // Bozuk Preferences verisi asla throw etmesin — her zaman false dönsün
        try
        {
            var actualBytes = Convert.FromHexString(actual);
            var expectedBytes = Convert.FromHexString(expected);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>PIN değiştir: önce mevcut PIN doğrulanır.</summary>
    public static bool ChangePin(string currentPin, string newPin)
    {
        if (!VerifyPin(currentPin))
            return false;
        SetPin(newPin);
        return true;
    }

    /// <summary>Kilidi tamamen kaldır (yöntem kapat + PIN temizle + oturum sıfırla).</summary>
    public static void DisableLock()
    {
        Method = AppLockMethod.None;
        Preferences.Default.Remove(PinSaltKey);
        Preferences.Default.Remove(PinHashKey);
        _unlockedInSession = false;
    }

    /// <summary>Oturum açık sayılır — sonraki Ayarlar girişleri kapıyı atlar.</summary>
    public static void MarkUnlocked() => _unlockedInSession = true;

    /// <summary>Ayarlar sekmesi girişinde kilit kapısı gerekiyor mu?</summary>
    public static bool NeedsSettingsUnlock =>
        IsAnyLockEnabled && AskOnSettingsEntry && !_unlockedInSession;

    private static string HashPin(string pin, string saltHex)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltHex + "|" + pin));
        return Convert.ToHexString(bytes);
    }
}
