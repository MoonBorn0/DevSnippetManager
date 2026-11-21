using Microsoft.Win32;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;

namespace DevSnippetManager.Services
{
    public static class ThemeService
    {
        private static string _selectedTheme = "Koyu";

        public static event EventHandler<bool>? EffectiveThemeChanged;

        static ThemeService()
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && _selectedTheme == "Sistem")
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher == null || dispatcher.CheckAccess())
                        Apply(_selectedTheme);
                    else
                        dispatcher.BeginInvoke(() => Apply(_selectedTheme));
                }
            };
        }

        public static bool IsSystemLight()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0) ?? 0) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool Apply(string theme)
        {
            _selectedTheme = theme is "Koyu" or "Açık" or "Sistem" ? theme : "Koyu";
            var isLight = _selectedTheme == "Açık" || (_selectedTheme == "Sistem" && IsSystemLight());
            var paletteHelper = new PaletteHelper();
            ITheme currentTheme = paletteHelper.GetTheme();
            currentTheme.SetBaseTheme(isLight ? MaterialDesignThemes.Wpf.Theme.Light : MaterialDesignThemes.Wpf.Theme.Dark);
            paletteHelper.SetTheme(currentTheme);
            EffectiveThemeChanged?.Invoke(null, isLight);
            return isLight;
        }

        public static string MonacoThemeFor(bool isLight) => isLight ? "vs" : "vs-dark";
    }
}
