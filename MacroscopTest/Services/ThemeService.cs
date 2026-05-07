using System.Windows;

namespace MacroscopTest.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    private const string LightThemeUri = "Themes/LightTheme.xaml";
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static void ApplyTheme(AppTheme theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var source = new Uri(theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri, UriKind.Relative);

        var currentDictionary = resources.MergedDictionaries
            .FirstOrDefault(dictionary => IsThemeDictionary(dictionary.Source));

        if (currentDictionary is not null)
        {
            currentDictionary.Source = source;
        }
        else
        {
            var dictionary = new ResourceDictionary { Source = source };
            resources.MergedDictionaries.Insert(0, dictionary);
        }

        CurrentTheme = theme;
    }

    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
    }

    private static bool IsThemeDictionary(Uri? source)
    {
        if (source is null)
        {
            return false;
        }

        var sourceValue = source.OriginalString;

        return sourceValue.EndsWith(LightThemeUri, StringComparison.OrdinalIgnoreCase) ||
               sourceValue.EndsWith(DarkThemeUri, StringComparison.OrdinalIgnoreCase);
    }
}
