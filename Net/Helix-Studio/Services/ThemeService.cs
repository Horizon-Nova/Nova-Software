using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace Helix_Studio.Services;

public sealed class ThemeService : IDisposable
{
    private const string LightUri = "Resources/Themes/Colors.xaml";
    private const string DarkUri = "Resources/Themes/Colors-Dark.xaml";

    private readonly AppSettingsService _settings;
    private SystemThemeWatcher? _watcher;

    public ThemeService(AppSettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize() => Apply(_settings.Settings.Theme);

    public void Apply(string theme)
    {
        _watcher?.Dispose();
        _watcher = null;

        if (theme == "system")
        {
            ApplyFromSystem();
            _watcher = new SystemThemeWatcher(() =>
                Application.Current?.Dispatcher.Invoke(ApplyFromSystem));
        }
        else
        {
            SwapDictionary(theme == "light" ? LightUri : DarkUri);
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private void ApplyFromSystem()
    {
        var isLight = ReadSystemThemeIsLight();
        SwapDictionary(isLight ? LightUri : DarkUri);
    }

    private static void SwapDictionary(string relativeUri)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Themes/Colors") == true);
        if (existing is null) return;
        merged.Remove(existing);
        merged.Add(new ResourceDictionary { Source = new Uri(relativeUri, UriKind.Relative) });
    }

    private static bool ReadSystemThemeIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
        }
        catch { return false; }
    }

    private sealed class SystemThemeWatcher : IDisposable
    {
        private readonly Action _onChange;

        public SystemThemeWatcher(Action onChange)
        {
            _onChange = onChange;
            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        }

        private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
                _onChange();
        }

        public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
    }
}
