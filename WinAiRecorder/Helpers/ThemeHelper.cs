using Microsoft.Win32;

namespace WinAiRecorder.Helpers;

public enum AppTheme { Dark, Light }

public static class ThemeHelper
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    public static AppTheme GetWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (key?.GetValue(RegistryValueName) is int value)
                return value == 1 ? AppTheme.Light : AppTheme.Dark;
        }
        catch { }
        return AppTheme.Dark;
    }

    public static AppTheme ResolveTheme(string themeSetting)
    {
        return themeSetting?.ToLowerInvariant() switch
        {
            "dark" => AppTheme.Dark,
            "light" => AppTheme.Light,
            _ => GetWindowsTheme()
        };
    }

}
