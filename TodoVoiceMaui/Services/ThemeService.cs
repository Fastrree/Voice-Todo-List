namespace TodoVoiceMaui.Services;

public static class ThemeService
{
    private const string ThemePreferenceKey = "theme_preference";

    public static void ApplyTheme(string? theme)
    {
        var preference = (theme ?? "system").ToLowerInvariant();
        switch (preference)
        {
            case "dark":
                Application.Current!.UserAppTheme = AppTheme.Dark;
                break;
            case "light":
                Application.Current!.UserAppTheme = AppTheme.Light;
                break;
            default:
                Application.Current!.UserAppTheme = AppTheme.Unspecified;
                break;
        }
    }

    public static void SaveTheme(string theme)
    {
        Preferences.Default.Set(ThemePreferenceKey, theme);
    }

    public static string GetSavedTheme()
    {
        return Preferences.Default.Get(ThemePreferenceKey, "light");
    }

    public static void ApplySavedTheme()
    {
        ApplyTheme(GetSavedTheme());
    }
}
