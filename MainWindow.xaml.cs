using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Velopack;
using BSG.Tools.Models;
using BSG.Tools.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace BSG.Tools
{
    public partial class MainWindow : Window
    {
        private string? _lastSuccessfulOutputPath;
        private UpdateInfo? _pendingUpdate;
        private DispatcherTimer? _toastTimer;

        public MainWindow()
        {
            InitializeComponent();
            // Theme's palette was already applied at startup (App.xaml.cs); this
            // paints this window's native title bar to match, since that's OS
            // chrome DynamicResource styling can't reach.
            ThemeManager.ApplyTitleBar(this);
            LoadSettingsIntoUi();
            UpdateSupplierInputsEnabled();
            UpdateExportButtonEnabled();
            UpdatePreviewButtonEnabled();
            TxtVersion.Text = $"v{GetCurrentVersion()}";
            _ = CheckForUpdatesAsync();
        }

        private static string GetCurrentVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }

        // ---------------- Auto-update ----------------

        private async Task CheckForUpdatesAsync()
        {
            var update = await UpdateService.CheckForUpdatesAsync();
            if (update is null)
                return;

            _pendingUpdate = update;
            TxtUpdateBanner.Text = string.Format(
                LocalizationManager.GetString("Str_UpdateAvailableFormat"),
                update.TargetFullRelease.Version);
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate is null)
                return;

            BtnUpdateNow.IsEnabled = false;
            BtnUpdateNow.Content = LocalizationManager.GetString("Str_Updating");

            var success = await UpdateService.DownloadAndApplyUpdateAsync(_pendingUpdate);

            if (!success)
            {
                BtnUpdateNow.IsEnabled = true;
                BtnUpdateNow.Content = LocalizationManager.GetString("Str_BtnUpdateNow");
                TxtUpdateBanner.Text = LocalizationManager.GetString("Str_UpdateFailed");
            }
            // Nếu thành công, ApplyUpdatesAndRestart() đã tự khởi động lại app — code sau điểm này sẽ không chạy.
        }

        // ---------------- Xuất file tab ----------------

        private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = LocalizationManager.GetString("Str_Dialog_ChooseSourceFile"),
                Filter = LocalizationManager.GetString("Str_ExcelFilter")
            };

            if (dialog.ShowDialog() == true)
            {
                TxtSourceFile.Text = dialog.FileName;
                UpdateExportButtonEnabled();
                UpdatePreviewButtonEnabled();
            }
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizationManager.GetString("Str_Dialog_SaveTemplate"),
                Filter = LocalizationManager.GetString("Str_ExcelFilter"),
                FileName = LocalizationManager.GetString("Str_TemplateFileName")
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                LabelExcelBuilder.BuildTemplate(dialog.FileName);
                _lastSuccessfulOutputPath = dialog.FileName;
                ClearStatus();
                ShowToast(LocalizationManager.GetString("Str_TemplateDownloaded"), showOpenFolder: true);
            }
            catch (Exception ex)
            {
                SetStatusError(string.Format(LocalizationManager.GetString("Str_TemplateCreateError"), ex.Message));
            }
        }

        private void BtnChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            // WPF has no built-in folder picker; System.Windows.Forms.FolderBrowserDialog
            // is available because the project sets <UseWindowsForms>true</UseWindowsForms>.
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtOutputFolder.Text = dialog.SelectedPath;
                UpdateExportButtonEnabled();
                UpdatePreviewButtonEnabled();
            }
        }

        private void ChkIncludeSupplier_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSupplierInputsEnabled();
        }

        private void UpdateSupplierInputsEnabled()
        {
            bool enabled = ChkIncludeSupplier.IsChecked == true;
            TxtSupplierName.IsEnabled = enabled;
            TxtSupplierAddress.IsEnabled = enabled;
            LblSupplierName.IsEnabled = enabled;
            LblSupplierAddress.IsEnabled = enabled;
        }

        private void UpdateExportButtonEnabled()
        {
            BtnExport.IsEnabled = File.Exists(TxtSourceFile.Text) && Directory.Exists(TxtOutputFolder.Text);
        }

        // Preview only reads the source file, so it doesn't need an output folder.
        private void UpdatePreviewButtonEnabled()
        {
            BtnPreview.IsEnabled = File.Exists(TxtSourceFile.Text);
        }

        /// <summary>
        /// Export inputs shared by direct export and export-from-preview: the
        /// supplier fields on this tab plus the saved label settings.
        /// </summary>
        private BuildOptions CreateBuildOptions()
        {
            var settings = SettingsService.Load();

            return new BuildOptions
            {
                IncludeSupplier = ChkIncludeSupplier.IsChecked == true,
                SupplierName = TxtSupplierName.Text.Trim(),
                SupplierAddress = TxtSupplierAddress.Text.Trim(),
                Importer = settings.Importer,
                ImporterAddress = settings.ImporterAddress,
                ProductionYear = settings.ProductionYear ?? "",
                UsageInstructions = settings.UsageInstructions,
                StorageInstructions = settings.StorageInstructions
            };
        }

        /// <summary>Output file path, or null when no valid output folder is chosen.</summary>
        private string? GetOutputPath()
        {
            if (string.IsNullOrWhiteSpace(TxtOutputFolder.Text) || !Directory.Exists(TxtOutputFolder.Text))
                return null;

            return Path.Combine(TxtOutputFolder.Text, GetOutputFileName());
        }

        // Known from the source file alone, so the preview can show it even before a folder is chosen.
        private string GetOutputFileName()
        {
            var sourceName = Path.GetFileNameWithoutExtension(TxtSourceFile.Text);
            var dateSuffix = DateTime.Now.ToString("ddMMyyyy");
            return $"temphu_{sourceName}_{dateSuffix}.xlsx";
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSourceFile.Text) || !File.Exists(TxtSourceFile.Text))
            {
                SetStatusError(LocalizationManager.GetString("Str_ValidSourceFileRequired"));
                return;
            }

            var outputPath = GetOutputPath();
            if (outputPath is null)
            {
                SetStatusError(LocalizationManager.GetString("Str_ValidOutputFolderRequired"));
                return;
            }

            try
            {
                LabelExcelBuilder.Build(TxtSourceFile.Text, outputPath, CreateBuildOptions());
                _lastSuccessfulOutputPath = outputPath;
                ClearStatus();
                ShowToast(LocalizationManager.GetString("Str_ExportSuccess"), showOpenFolder: true);
            }
            catch (Exception ex)
            {
                SetStatusError(string.Format(LocalizationManager.GetString("Str_ExportError"), ex.Message));
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSourceFile.Text) || !File.Exists(TxtSourceFile.Text))
            {
                SetStatusError(LocalizationManager.GetString("Str_ValidSourceFileRequired"));
                return;
            }

            // Source-file problems (missing column, no products, file locked)
            // are reported here instead of opening an empty/broken preview.
            IReadOnlyList<LabelEntry> labels;
            try
            {
                labels = LabelExcelBuilder.LoadLabels(TxtSourceFile.Text, CreateBuildOptions());
            }
            catch (Exception ex)
            {
                SetStatusError(string.Format(LocalizationManager.GetString("Str_PreviewLoadError"), ex.Message));
                return;
            }

            ClearStatus();
            var preview = new PreviewWindow(labels, GetOutputFileName(), GetOutputPath()) { Owner = this };
            if (preview.ShowDialog() == true && preview.ExportedPath is not null)
            {
                _lastSuccessfulOutputPath = preview.ExportedPath;
                ShowToast(LocalizationManager.GetString("Str_ExportSuccess"), showOpenFolder: true);
            }
        }

        private void ClearStatus()
        {
            TxtStatus.Text = "";
            TxtStatus.Cursor = System.Windows.Input.Cursors.Arrow;
            TxtStatus.TextDecorations = null;
        }

        private void SetStatusError(string message)
        {
            _lastSuccessfulOutputPath = null;
            TxtStatus.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BrushStatusError"];
            TxtStatus.Text = message;
            TxtStatus.Cursor = System.Windows.Input.Cursors.Arrow;
            TxtStatus.TextDecorations = null;
        }

        private void TxtStatus_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_lastSuccessfulOutputPath is null)
                return;

            OpenContainingFolder(_lastSuccessfulOutputPath);
        }

        private void BtnToastOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSuccessfulOutputPath is null)
                return;

            OpenContainingFolder(_lastSuccessfulOutputPath);
        }

        private void OpenContainingFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                SetStatusError(LocalizationManager.GetString("Str_OpenFolderError"));
            }
        }

        private void ShowToast(string message, bool showOpenFolder)
        {
            TxtToastMessage.Text = message;
            BtnToastOpenFolder.Visibility = showOpenFolder ? Visibility.Visible : Visibility.Collapsed;
            Toast.Visibility = Visibility.Visible;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _toastTimer.Tick += (_, _) =>
            {
                Toast.Visibility = Visibility.Collapsed;
                _toastTimer!.Stop();
            };
            _toastTimer.Start();
        }

        // ---------------- Cài đặt tab ----------------

        private void LoadSettingsIntoUi()
        {
            var settings = SettingsService.Load();
            TxtImporter.Text = settings.Importer;
            TxtImporterAddress.Text = settings.ImporterAddress;
            // Show the current year as a placeholder default, but keep it
            // editable/custom if the user already saved a specific value.
            TxtProductionYear.Text = string.IsNullOrWhiteSpace(settings.ProductionYear)
                ? DateTime.Now.Year.ToString()
                : settings.ProductionYear;
            TxtUsageInstructions.Text = settings.UsageInstructions;
            TxtStorageInstructions.Text = settings.StorageInstructions;

            // Theme/language were already applied at startup (App.xaml.cs); this
            // just syncs the controls so they reflect the saved preference.
            ChkDarkMode.IsChecked = settings.Theme == ThemeManager.Dark;
            foreach (System.Windows.Controls.ComboBoxItem item in CmbLanguage.Items)
            {
                if ((string)item.Tag == settings.Language)
                {
                    CmbLanguage.SelectedItem = item;
                    break;
                }
            }
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
            {
                Importer = TxtImporter.Text.Trim(),
                ImporterAddress = TxtImporterAddress.Text.Trim(),
                ProductionYear = TxtProductionYear.Text.Trim(),
                UsageInstructions = TxtUsageInstructions.Text.Trim(),
                StorageInstructions = TxtStorageInstructions.Text.Trim(),
                Theme = ChkDarkMode.IsChecked == true ? ThemeManager.Dark : ThemeManager.Light,
                Language = CurrentLanguageTag()
            };

            SettingsService.Save(settings);
            _lastSuccessfulOutputPath = null;
            ClearStatus();
            ShowToast(LocalizationManager.GetString("Str_SettingsSaved"), showOpenFolder: false);
        }

        private void ChkDarkMode_Changed(object sender, RoutedEventArgs e)
        {
            var theme = ChkDarkMode.IsChecked == true ? ThemeManager.Dark : ThemeManager.Light;
            ThemeManager.Apply(theme);

            // Persisted immediately (not gated behind "Lưu cài đặt") since this is
            // an app-wide display preference, not label content.
            var settings = SettingsService.Load();
            settings.Theme = theme;
            SettingsService.Save(settings);
        }

        private string CurrentLanguageTag() =>
            (string)((System.Windows.Controls.ComboBoxItem)CmbLanguage.SelectedItem).Tag;

        private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbLanguage.SelectedItem is null)
                return;

            var language = CurrentLanguageTag();
            LocalizationManager.Apply(language);

            // Persisted immediately (not gated behind "Lưu cài đặt") since this is
            // an app-wide display preference, not label content — same as dark mode.
            var settings = SettingsService.Load();
            settings.Language = language;
            SettingsService.Save(settings);
        }

        private void TxtProductionYear_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void TxtProductionYear_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text) ||
                !((string)e.DataObject.GetData(System.Windows.DataFormats.Text)).All(char.IsDigit))
            {
                e.CancelCommand();
            }
        }

        private void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                LocalizationManager.GetString("Str_RestoreDefaultsConfirm"),
                LocalizationManager.GetString("Str_RestoreDefaultsTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // "Restore defaults" only resets the label-content fields below —
            // dark mode/language are separate display preferences, left untouched.
            var defaults = new AppSettings
            {
                Theme = ChkDarkMode.IsChecked == true ? ThemeManager.Dark : ThemeManager.Light,
                Language = CurrentLanguageTag()
            };
            SettingsService.Save(defaults);

            TxtImporter.Text = defaults.Importer;
            TxtImporterAddress.Text = defaults.ImporterAddress;
            TxtProductionYear.Text = DateTime.Now.Year.ToString();
            TxtUsageInstructions.Text = defaults.UsageInstructions;
            TxtStorageInstructions.Text = defaults.StorageInstructions;

            ShowToast(LocalizationManager.GetString("Str_DefaultsRestored"), showOpenFolder: false);
        }
    }
}
