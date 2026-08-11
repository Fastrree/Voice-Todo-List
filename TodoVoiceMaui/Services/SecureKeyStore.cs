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
