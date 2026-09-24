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

        /// <summary>Swaps the string dictionary (index 1 in App.xaml); DynamicResource text updates at once.</summary>
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

        /// <summary>Localized string for code-behind (status text, MessageBox, dialogs).</summary>
        public static string GetString(string key) =>
            Application.Current.Resources[key] as string ?? key;
    }
}
