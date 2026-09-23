using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using BSG.Tools.Services;

namespace BSG.Tools
{
    /// <summary>
    /// Modal preview of the labels an export would write. Holds the already
    /// built labels, so exporting from here writes exactly what was shown -
    /// the source file is not re-read.
    /// </summary>
    public partial class PreviewWindow : Window
    {
        private readonly IReadOnlyList<LabelEntry> _labels;
        private readonly string? _outputPath;

        /// <summary>Set once an export from this window succeeds.</summary>
        public string? ExportedPath { get; private set; }

        /// <param name="outputFileName">File name the export will use, shown even without a folder.</param>
        /// <param name="outputPath">Target file, or null when no valid output folder is chosen yet.</param>
        public PreviewWindow(IReadOnlyList<LabelEntry> labels, string outputFileName, string? outputPath)
        {
            InitializeComponent();
            ThemeManager.ApplyTitleBar(this);

            _labels = labels;
            _outputPath = outputPath;

            LabelList.ItemsSource = labels;
            ShowSummary();
            TxtFileName.Text = string.Format(LocalizationManager.GetString("Str_PreviewFileNameFormat"), outputFileName);
            TxtFileName.ToolTip = outputPath; // full path on hover, once a folder is chosen
            ShowEmptyFieldWarning();

            BtnPreviewExport.IsEnabled = outputPath is not null;
            if (outputPath is null)
                TxtPreviewStatus.Text = LocalizationManager.GetString("Str_PreviewNoOutputFolder");
        }

        /// <summary>
        /// "N products · Total quantity: X". Quantity is free text in the source
        /// file (SL column), so only whole numbers are summed; the rest are
        /// counted separately instead of silently dropped.
        /// </summary>
        private void ShowSummary()
        {
            long total = 0;
            int invalid = 0;
            foreach (var label in _labels)
            {
                // Thousands separators ("1,000" / "1.000") are stripped - SL is a count, never fractional.
                var digits = label.Quantity.Trim().Replace(",", "").Replace(".", "").Replace(" ", "");
                if (long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var qty))
                    total += qty;
                else
                    invalid++;
            }

            var text = string.Format(LocalizationManager.GetString("Str_PreviewCountFormat"), _labels.Count)
                + " · "
                + string.Format(LocalizationManager.GetString("Str_PreviewTotalQuantityFormat"), total.ToString("N0", CultureInfo.CurrentCulture));
            TxtCount.Text = text;

            if (invalid > 0)
            {
                TxtInvalidQuantity.Text = string.Format(LocalizationManager.GetString("Str_PreviewInvalidQuantityFormat"), invalid);
                TxtInvalidQuantity.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Warns about fields that are empty on every label (typically a blank
        /// setting or supplier field). A field empty on only some products is
        /// just highlighted on those lines, so the banner stays short.
        /// </summary>
        private void ShowEmptyFieldWarning()
        {
            var emptyEverywhere = _labels[0].Lines
                .Select(l => l.Label)
                .Where(label => _labels.All(e => e.Lines.Any(l => l.Label == label && l.IsEmpty)))
                .ToList();

            if (emptyEverywhere.Count == 0)
                return;

            TxtWarning.Text = string.Format(
                LocalizationManager.GetString("Str_PreviewEmptyFieldsFormat"),
                string.Join(", ", emptyEverywhere));
            WarningBanner.Visibility = Visibility.Visible;
        }

        private void BtnPreviewExport_Click(object sender, RoutedEventArgs e)
        {
            if (_outputPath is null)
                return;

            try
            {
                LabelExcelBuilder.Build(_labels, _outputPath);
                ExportedPath = _outputPath;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                // Stay open so the user can fix the cause (e.g. close the file in Excel) and retry.
                TxtPreviewStatus.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BrushStatusError"];
                TxtPreviewStatus.Text = string.Format(LocalizationManager.GetString("Str_ExportError"), ex.Message);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
