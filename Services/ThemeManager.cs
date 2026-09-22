using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace BSG.Tools.Services
{
    public static class ThemeManager
    {
        public const string Light = "Light";
        public const string Dark = "Dark";

        // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Windows 10 20H1+/Windows 11,
        // 19 on earlier Windows 10 builds that still support the attribute.
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public static bool IsDark { get; private set; }

        /// <summary>
        /// Swaps the app's theme resource dictionary (index 0 of the merged
        /// dictionaries in App.xaml) for the requested theme. Styles reference
        /// the palette brushes via DynamicResource, so every open window's
        /// client area repaints immediately. Also repaints each open window's
        /// native title bar, which DynamicResource cannot reach (Windows draws
        /// it itself).
        /// </summary>
        public static void Apply(string? theme)
        {
            IsDark = theme == Dark;

            var uri = new Uri(
                IsDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
                UriKind.Relative);

            var themeDictionary = new ResourceDictionary { Source = uri };
            var merged = Application.Current.Resources.MergedDictionaries;

            if (merged.Count > 0)
                merged[0] = themeDictionary;
            else
                merged.Add(themeDictionary);

            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);
        }

        /// <summary>
        /// Paints a single window's native title bar to match the current theme
        /// (see <see cref="IsDark"/>). Forces the window's handle to exist if it
        /// doesn't yet, so this can be called right after InitializeComponent —
        /// before the window is shown — to avoid a light-then-dark flash.
        /// </summary>
        public static void ApplyTitleBar(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).EnsureHandle();
                int useDark = IsDark ? 1 : 0;

                if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));
            }
            catch
            {
                // Older Windows versions without this DWM attribute: title bar
                // just stays the OS default. Not worth failing startup over.
            }
        }
    }
}
