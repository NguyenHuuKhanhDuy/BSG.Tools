using System;
using System.Windows;
using Application = System.Windows.Application;

namespace BSG.Tools.Services
{
    public static class LocalizationManager
    {
        public const string Vietnamese = "vi";
        public const string English = "en";

        public static string Current { get; private set; } = Vietnamese;

        /// <summary>
        /// Swaps the app's string resource dictionary (index 1 of the merged
        /// dictionaries in App.xaml, right after the theme at index 0) for the
        /// requested language. XAML text references the strings via
        /// DynamicResource, so every open window repaints its text immediately.
        /// </summary>
        public static void Apply(string? language)
        {
            Current = language == English ? English : Vietnamese;

            var uri = new Uri(
                Current == English ? "Localization/Strings.en.xaml" : "Localization/Strings.vi.xaml",
                UriKind.Relative);

            var stringsDictionary = new ResourceDictionary { Source = uri };
            var merged = Application.Current.Resources.MergedDictionaries;

            if (merged.Count > 1)
                merged[1] = stringsDictionary;
            else
                merged.Add(stringsDictionary);
        }

        /// <summary>
        /// Looks up a localized string for use from code-behind (status
        /// messages, MessageBox text, dialog titles) where DynamicResource
        /// markup isn't available.
        /// </summary>
        public static string GetString(string key) =>
            Application.Current.Resources[key] as string ?? key;
    }
}
