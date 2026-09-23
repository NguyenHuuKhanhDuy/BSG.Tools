using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BSG.Tools.Services
{
    /// <summary>
    /// Builds the export file name from a user pattern. Supported placeholders:
    /// {source} (source file name without extension), {date} (ddMMyyyy) and
    /// {date:format} (any .NET date format). Unknown placeholders are kept as text.
    /// </summary>
    public static class ExportFileNameFormatter
    {
        public const string DefaultPattern = "temphu_{source}_{date}";
        private const string DefaultDateFormat = "ddMMyyyy";
        private const string Extension = ".xlsx";

        private static readonly Regex Placeholder = new(@"\{(source|date)(?::([^}]*))?\}", RegexOptions.IgnoreCase);

        public static string Format(string? pattern, string sourceFileName, DateTime now)
        {
            var name = Expand(string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern, sourceFileName, now);
            if (string.IsNullOrWhiteSpace(name))
                name = Expand(DefaultPattern, sourceFileName, now);

            return name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? name : name + Extension;
        }

        private static string Expand(string pattern, string sourceFileName, DateTime now)
        {
            var source = Path.GetFileNameWithoutExtension(sourceFileName);
            var expanded = Placeholder.Replace(pattern.Trim(), m =>
            {
                if (m.Groups[1].Value.Equals("source", StringComparison.OrdinalIgnoreCase))
                    return source;

                var format = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : DefaultDateFormat;
                try
                {
                    return now.ToString(format);
                }
                catch (FormatException)
                {
                    return now.ToString(DefaultDateFormat);
                }
            });

            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(expanded.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            // Windows silently strips trailing dots/spaces from file names.
            return safe.TrimEnd('.', ' ');
        }
    }
}
