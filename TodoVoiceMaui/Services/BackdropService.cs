namespace TodoVoiceMaui.Services;

public enum BackdropKind
{
    Unknown,
    Mica,
    Acrylic,
    Fallback
}

public static class BackdropService
{
    private static BackdropKind _active = BackdropKind.Unknown;
    public static BackdropKind Active => _active;

    /// <summary>
    /// Liquid Glass KATMAN 0 (transition-framework.md §2.3):
    /// pencere seviyesinde Mica → DesktopAcrylic → fallback zinciri.
    /// Windows 11 + WinAppSDK 1.3+ gerekir; diğer platformlarda no-op.
    /// </summary>
    public static void ApplyTo(Window window)
    {
#if WINDOWS
        try
        {
            var hwnd = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (hwnd == null) { _active = BackdropKind.Fallback; return; }

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(hwnd);
            var winuiWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle));

            if (winuiWindow is null) { _active = BackdropKind.Fallback; return; }

            var systemBackdrop = hwnd.SystemBackdrop as Microsoft.UI.Xaml.Media.MicaBackdrop;
            var acrylicBackdrop = hwnd.SystemBackdrop as Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop;

            if (systemBackdrop == null && acrylicBackdrop == null)
            {
                // Mica önce (masaüstü arka planını bulanıklaştırır)
                try
                {
                    hwnd.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                    _active = BackdropKind.Mica;
                }
                catch
                {
                    // Mica yoksa Desktop Acrylic
                    try
                    {
                        hwnd.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                        _active = BackdropKind.Acrylic;
                    }
                    catch
                    {
                        _active = BackdropKind.Fallback;
                    }
                }
            }
            else if (systemBackdrop != null)
            {
                _active = BackdropKind.Mica;
            }
            else
            {
                _active = BackdropKind.Acrylic;
            }
        }
        catch
        {
            _active = BackdropKind.Fallback;
        }
#else
        _active = BackdropKind.Fallback;
#endif
    }
}
