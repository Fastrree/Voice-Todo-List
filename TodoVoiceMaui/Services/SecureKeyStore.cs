using System.Runtime.InteropServices;
using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>
/// API anahtarlarını Windows DPAPI (Data Protection API) ile şifreler.
/// `CryptProtectData` anahtarı KULLANICININ Windows hesabına bağlar — diskteki
/// düz metin okuma (başka kullanıcı, yedek kopya, kötü amaçlı yazılım tarafından
/// kolay okunma) engellenir. NuGet paketi GEREKTİRMEZ (saf crypt32.dll P/Invoke).
///
/// Saklama: DPAPI çıktısı Base64 olarak Preferences'ta tutulur (CloudTranscribers
/// "enc:" önekiyle). Şifreleme çözülemezse (örn. hesap taşındı) eski değer korunur.
/// </summary>
public static class SecureKeyStore
{
    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags, out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll")]
    private static extern void LocalFree(IntPtr hMem);

    /// <summary>Düz metni şifreler, Base64 döner. Windows dışında/hatada null.</summary>
    public static string? Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext) || !OperatingSystem.IsWindows())
            return plaintext;

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var input = new DATA_BLOB { cbData = bytes.Length, pbData = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.pbData, bytes.Length);

            if (!CryptProtectData(ref input, "TodoVoice API Key", IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out var output))
                return null;

            try
            {
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return Convert.ToBase64String(result);
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
        }
    }

    /// <summary>Şifreli Base64'i çözer. Windows dışında/hatada null.</summary>
    public static string? Unprotect(string base64)
    {
        if (string.IsNullOrEmpty(base64) || !OperatingSystem.IsWindows())
            return base64;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }

        var input = new DATA_BLOB { cbData = bytes.Length, pbData = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.pbData, bytes.Length);

            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out var output))
                return null;

            try
            {
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return Encoding.UTF8.GetString(result);
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
            }
        }
        finally
        {
            if (input.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(input.pbData);
        }
    }
}

/// <summary>
/// Windows Credential Manager (Windows Vault) — API anahtarlarının BİRİNCİL deposu.
/// `CredWrite/CredRead/CredDelete` (advapi32) her Windows işleminde çalışır (paket
/// kimliği GEREKMEZ — unpackaged WinUI 3 uygulamamızda doğrulanmıştır).
///
/// Güvenlik: Windows, credential blob'u kullanıcının oturumuna bağlı anahtarla
/// (LSA/DPAPI) diskte otomatik şifreler — uygulama katmanında ek şifreleme gerekmez.
/// DPAPI-in-Preferences'a göre avantajı: Windows'un kendi güvenli deposu, başka
/// uygulamalar/birleşik yönetim (cmdkey, Denetim Masası) tarafından görülebilir/yönetilebilir.
/// </summary>
public static class WindowsCredentialStore
{
    private const string AdvApi32 = "advapi32.dll";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2; // makineye özel, oturumlar arası kalıcı
    private const int ErrorNotFound = 1168;
    private const string DefaultUserName = "TodoVoice";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport(AdvApi32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW([In] ref CREDENTIALW userCredential, [In] uint flags);

    [DllImport(AdvApi32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string targetName, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport(AdvApi32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string targetName, uint type, uint reservedFlag);

    [DllImport(AdvApi32, SetLastError = true)]
    private static extern bool CredFree([In] IntPtr cred);

    /// <summary>Anahtarı Windows Vault'a yazar. Başarısızsa false (arayan DPAPI'ye düşer).</summary>
    public static bool Save(string targetName, string secret)
    {
        if (string.IsNullOrEmpty(targetName) || string.IsNullOrEmpty(secret) || !OperatingSystem.IsWindows())
            return false;

        var blobBytes = Encoding.UTF8.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(blobBytes.Length);
        try
        {
            Marshal.Copy(blobBytes, 0, blobPtr, blobBytes.Length);

            var cred = new CREDENTIALW
            {
                Flags = 0,
                Type = CredTypeGeneric,
                TargetName = targetName,
                Comment = "TodoVoice STT API key",
                UserName = DefaultUserName,
                CredentialBlobSize = (uint)blobBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null!
            };

            return CredWriteW(ref cred, 0);
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    /// <summary>Anahtarı okur. Kayıt yoksa / hata olursa null.</summary>
    public static string? Read(string targetName)
    {
        if (string.IsNullOrEmpty(targetName) || !OperatingSystem.IsWindows())
            return null;

        if (!CredReadW(targetName, CredTypeGeneric, 0, out var pCred))
        {
            // ERROR_NOT_FOUND normaldir (kayıt hiç yazılmamış) — null döner, hata değil
            return null;
        }

        try
        {
            var nativeCred = Marshal.PtrToStructure<CREDENTIALW>(pCred);
            if (nativeCred.CredentialBlob == IntPtr.Zero || nativeCred.CredentialBlobSize == 0)
                return null;

            var blobBytes = new byte[nativeCred.CredentialBlobSize];
            Marshal.Copy(nativeCred.CredentialBlob, blobBytes, 0, (int)nativeCred.CredentialBlobSize);
            return Encoding.UTF8.GetString(blobBytes);
        }
        catch
        {
            return null;
        }
        finally
        {
            CredFree(pCred); // tek parça tahsis — yalnızca CredFree yeterli
        }
    }

    /// <summary>Kaydı siler (yoksa zaten başarılı sayılır).</summary>
    public static void Delete(string targetName)
    {
        if (string.IsNullOrEmpty(targetName) || !OperatingSystem.IsWindows())
            return;

        try
        {
            CredDeleteW(targetName, CredTypeGeneric, 0);
        }
        catch { }
    }
}
