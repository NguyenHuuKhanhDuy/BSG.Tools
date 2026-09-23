using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private enum StatusKind { Ready, Busy, Success, Error }

        private string? _lastSuccessfulOutputPath;
        private UpdateInfo? _pendingUpdate;
        private DispatcherTimer? _toastTimer;
        private DispatcherTimer? _statusTimer;

        // True while a preview load or export runs in the background; blocks starting another.
        private bool _isBusy;
        private StatusKind _statusKind = StatusKind.Ready;
        // Re-evaluated on every render so the message follows a language switch.
        private Func<string>? _statusMessage;

        // Edited copy of the label field configuration; only persisted on "Lưu cài đặt".
        private readonly ObservableCollection<LabelFieldRow> _labelFieldRows = new();
        // Last saved (or loaded) settings; the Settings tab is compared against it to find unsaved changes.
        private AppSettings? _savedSettings;

        public MainWindow()
        {
            InitializeComponent();
            LabelFieldList.ItemsSource = _labelFieldRows;
            // Reordering (drag & drop, Alt+Up/Down) changes positions, which count as unsaved changes.
            _labelFieldRows.CollectionChanged += (_, _) => UpdateUnsavedState();
            // Theme's palette was already applied at startup (App.xaml.cs); this
            // paints this window's native title bar to match, since that's OS
            // chrome DynamicResource styling can't reach.
            ThemeManager.ApplyTitleBar(this);
            LoadSettingsIntoUi();
            UpdateSupplierInputsEnabled();
            UpdateExportButtonEnabled();
            UpdatePreviewButtonEnabled();
            TxtVersion.Text = $"v{GetCurrentVersion()}";
            ClearStatus();
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

        // ---------------- Tabs ----------------

        // Clicking a tab header normally moves focus into the first input of that tab: WPF's
        // TabItem focuses its content unless focus is already on a sibling tab header. Parking
        // focus on the current header first makes the click just select the tab.
        private void MainTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source is not null and not System.Windows.Controls.TabItem)
                source = source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : null;

            if (source is System.Windows.Controls.TabItem { IsSelected: false }
                && MainTabs.SelectedItem is System.Windows.Controls.TabItem current)
            {
                current.Focus();
            }
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
                UpdateFileNameExample();
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
                SetStatus(StatusKind.Success, () => LocalizationManager.GetString("Str_TemplateDownloaded"));
                ShowToast(LocalizationManager.GetString("Str_TemplateDownloaded"), showOpenFolder: true);
            }
            catch (Exception ex)
            {
                SetStatusError("Str_TemplateCreateError", ex.Message);
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
            BtnExport.IsEnabled = !_isBusy && File.Exists(TxtSourceFile.Text) && Directory.Exists(TxtOutputFolder.Text);
        }

        // Preview only reads the source file, so it doesn't need an output folder.
        private void UpdatePreviewButtonEnabled()
        {
            BtnPreview.IsEnabled = !_isBusy && File.Exists(TxtSourceFile.Text);
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
                StorageInstructions = settings.StorageInstructions,
                LabelFields = LabelFieldCatalog.Normalize(settings.LabelFields)
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
        private string GetOutputFileName() =>
            ExportFileNameFormatter.Format(SettingsService.Load().ExportFileNamePattern, TxtSourceFile.Text, DateTime.Now);

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (string.IsNullOrWhiteSpace(TxtSourceFile.Text) || !File.Exists(TxtSourceFile.Text))
            {
                SetStatusError("Str_ValidSourceFileRequired");
                return;
            }

            var outputPath = GetOutputPath();
            if (outputPath is null)
            {
                SetStatusError("Str_ValidOutputFolderRequired");
                return;
            }

            // Read every control on the UI thread before handing the work off.
            var sourcePath = TxtSourceFile.Text;
            var options = CreateBuildOptions();

            SetBusy(true);
            SetStatus(StatusKind.Busy, () => LocalizationManager.GetString("Str_StatusExporting"));
            try
            {
                var count = await Task.Run(() =>
                {
                    var labels = LabelExcelBuilder.LoadLabels(sourcePath, options);
                    LabelExcelBuilder.Build(labels, outputPath);
                    return labels.Count;
                });
                _lastSuccessfulOutputPath = outputPath;
                SetExportedStatus(count);
                ShowToast(LocalizationManager.GetString("Str_ExportSuccess"), showOpenFolder: true);
            }
            catch (Exception ex)
            {
                SetStatusError("Str_ExportError", ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            if (string.IsNullOrWhiteSpace(TxtSourceFile.Text) || !File.Exists(TxtSourceFile.Text))
            {
                SetStatusError("Str_ValidSourceFileRequired");
                return;
            }

            var sourcePath = TxtSourceFile.Text;
            var options = CreateBuildOptions();

            // Source-file problems (missing column, no products, file locked)
            // are reported here instead of opening an empty/broken preview.
            IReadOnlyList<LabelEntry> labels;
            SetBusy(true);
            SetStatus(StatusKind.Busy, () => LocalizationManager.GetString("Str_StatusReading"));
            try
            {
                labels = await Task.Run(() => LabelExcelBuilder.LoadLabels(sourcePath, options));
            }
            catch (Exception ex)
            {
                SetStatusError("Str_PreviewLoadError", ex.Message);
                return;
            }
            finally
            {
                SetBusy(false);
            }

            ClearStatus();
            var preview = new PreviewWindow(labels, GetOutputFileName(), GetOutputPath()) { Owner = this };
            if (preview.ShowDialog() == true && preview.ExportedPath is not null)
            {
                _lastSuccessfulOutputPath = preview.ExportedPath;
                SetExportedStatus(labels.Count);
                ShowToast(LocalizationManager.GetString("Str_ExportSuccess"), showOpenFolder: true);
            }
        }

        // ---------------- Status bar ----------------

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            UpdateExportButtonEnabled();
            UpdatePreviewButtonEnabled();
        }

        private void ClearStatus() => SetStatus(StatusKind.Ready, null);

        private void SetExportedStatus(int count) =>
            SetStatus(StatusKind.Success,
                () => string.Format(LocalizationManager.GetString("Str_StatusExported"), count));

        /// <summary>Shows a localized error: <paramref name="key"/> is a string resource key, formatted with <paramref name="args"/>.</summary>
        private void SetStatusError(string key, params object[] args)
        {
            _lastSuccessfulOutputPath = null;
            SetStatus(StatusKind.Error, () => string.Format(LocalizationManager.GetString(key), args));
        }

        private void SetStatus(StatusKind kind, Func<string>? message)
        {
            _statusKind = kind;
            _statusMessage = message;
            RenderStatus();

            // Success is a transient confirmation; errors stay until the next action so they can be read.
            _statusTimer?.Stop();
            if (kind == StatusKind.Success)
            {
                _statusTimer ??= CreateStatusTimer();
                _statusTimer.Start();
            }
        }

        private DispatcherTimer CreateStatusTimer()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ClearStatus();
            };
            return timer;
        }

        private void RenderStatus()
        {
            var brushKey = _statusKind switch
            {
                StatusKind.Busy => "BrushStatusBusy",
                StatusKind.Success => "BrushStatusSuccess",
                StatusKind.Error => "BrushStatusError",
                _ => "BrushTextSecondary"
            };
            // Resource references (not a one-off lookup) so the colours follow a theme switch.
            TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, brushKey);
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brushKey);

            TxtStatus.Text = _statusMessage?.Invoke() ?? LocalizationManager.GetString("Str_StatusReady");
            // A success message can be clicked to open the output folder.
            TxtStatus.Cursor = _statusKind == StatusKind.Success && _lastSuccessfulOutputPath is not null
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.Arrow;
            TxtStatus.TextDecorations = null;
        }

        private void TxtStatus_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_statusKind != StatusKind.Success || _lastSuccessfulOutputPath is null)
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
                SetStatusError("Str_OpenFolderError");
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
            _savedSettings = settings;
            TxtImporter.Text = settings.Importer;
            TxtImporterAddress.Text = settings.ImporterAddress;
            // Show the current year as a placeholder default, but keep it
            // editable/custom if the user already saved a specific value.
            TxtProductionYear.Text = string.IsNullOrWhiteSpace(settings.ProductionYear)
                ? DateTime.Now.Year.ToString()
                : settings.ProductionYear;
            TxtUsageInstructions.Text = settings.UsageInstructions;
            TxtStorageInstructions.Text = settings.StorageInstructions;
            TxtFileNamePattern.Text = settings.ExportFileNamePattern;
            LoadLabelFieldRows(settings.LabelFields);

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

            UpdateUnsavedState();
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
                ExportFileNamePattern = TxtFileNamePattern.Text.Trim(),
                LabelFields = _labelFieldRows.Select(r => r.ToSetting()).ToList(),
                Theme = ChkDarkMode.IsChecked == true ? ThemeManager.Dark : ThemeManager.Light,
                Language = CurrentLanguageTag()
            };

            SettingsService.Save(settings);
            _savedSettings = settings;
            UpdateUnsavedState();
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
            // Status and file name example are set from code, so they don't follow DynamicResource on their own.
            RenderStatus();
            UpdateFileNameExample();

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
            _savedSettings = defaults;

            TxtImporter.Text = defaults.Importer;
            TxtImporterAddress.Text = defaults.ImporterAddress;
            TxtProductionYear.Text = DateTime.Now.Year.ToString();
            TxtUsageInstructions.Text = defaults.UsageInstructions;
            TxtStorageInstructions.Text = defaults.StorageInstructions;
            TxtFileNamePattern.Text = defaults.ExportFileNamePattern;
            LoadLabelFieldRows(defaults.LabelFields);
            UpdateUnsavedState();

            ShowToast(LocalizationManager.GetString("Str_DefaultsRestored"), showOpenFolder: false);
        }

        // ---------------- Unsaved changes (Cài đặt tab) ----------------

        private void SettingsField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            UpdateUnsavedState();

        private void LabelFieldRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // IsModified/IsDragging are set by this tracking and by drag & drop; reacting to them would loop.
            if (e.PropertyName is nameof(LabelFieldRow.Enabled) or nameof(LabelFieldRow.Label))
                UpdateUnsavedState();
        }

        /// <summary>
        /// Compares every Settings-tab field with the last saved settings: marks the ones that
        /// differ and enables "Lưu cài đặt" only when at least one does. Dark mode and language
        /// save themselves immediately, so they are not part of this.
        /// </summary>
        private void UpdateUnsavedState()
        {
            if (_savedSettings is not { } saved)
                return;

            // A blank saved year is shown as the current year, so that is what "unchanged" looks like.
            var savedYear = string.IsNullOrWhiteSpace(saved.ProductionYear) ? DateTime.Now.Year.ToString() : saved.ProductionYear;

            bool unsaved = false;
            unsaved |= MarkModified(TxtImporter, saved.Importer);
            unsaved |= MarkModified(TxtImporterAddress, saved.ImporterAddress);
            unsaved |= MarkModified(TxtProductionYear, savedYear);
            unsaved |= MarkModified(TxtUsageInstructions, saved.UsageInstructions);
            unsaved |= MarkModified(TxtStorageInstructions, saved.StorageInstructions);
            unsaved |= MarkModified(TxtFileNamePattern, saved.ExportFileNamePattern);

            var savedFields = LabelFieldCatalog.Normalize(saved.LabelFields);
            for (int i = 0; i < _labelFieldRows.Count; i++)
            {
                var row = _labelFieldRows[i];
                var savedIndex = savedFields.FindIndex(f => f.Key == row.Key);
                row.IsModified = savedIndex != i
                    || savedFields[savedIndex].Enabled != row.Enabled
                    || savedFields[savedIndex].Label.Trim() != row.Label.Trim();
                unsaved |= row.IsModified;
            }

            BtnSaveSettings.IsEnabled = unsaved;
        }

        // Values are trimmed on save, so surrounding whitespace alone doesn't count as a change.
        private static bool MarkModified(System.Windows.Controls.TextBox box, string? savedValue)
        {
            var modified = box.Text.Trim() != (savedValue ?? "").Trim();
            ModifiedState.SetIsModified(box, modified);
            return modified;
        }

        // ---------------- Export format (Cài đặt tab) ----------------

        private void LoadLabelFieldRows(IEnumerable<LabelFieldSetting>? fields)
        {
            foreach (var old in _labelFieldRows)
                old.PropertyChanged -= LabelFieldRow_PropertyChanged;
            _labelFieldRows.Clear();

            foreach (var field in LabelFieldCatalog.Normalize(fields))
            {
                var row = new LabelFieldRow(field);
                row.PropertyChanged += LabelFieldRow_PropertyChanged;
                _labelFieldRows.Add(row);
            }
        }

        // Keyboard alternative to drag & drop: Alt+Up / Alt+Down moves the row that has focus.
        private void LabelFieldList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // With Alt held, WPF reports the arrow in SystemKey and Key is Key.System.
            if (e.Key != Key.System || (e.SystemKey != Key.Up && e.SystemKey != Key.Down))
                return;
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not LabelFieldRow row)
                return;

            e.Handled = true;
            var from = _labelFieldRows.IndexOf(row);
            var to = from + (e.SystemKey == Key.Up ? -1 : 1);
            if (from < 0 || to < 0 || to >= _labelFieldRows.Count)
                return;

            var focusedType = e.OriginalSource.GetType();
            _labelFieldRows.Move(from, to);

            // The moved row's container is regenerated, so put focus back on the same kind of
            // control (label box or checkbox) in its new position once layout has caught up.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (LabelFieldList.ItemContainerGenerator.ContainerFromIndex(to) is DependencyObject container)
                    FindDescendant(container, focusedType)?.Focus();
            });
        }

        private static UIElement? FindDescendant(DependencyObject parent, Type type)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement element && type.IsInstanceOfType(child))
                    return element;
                if (FindDescendant(child, type) is { } found)
                    return found;
            }
            return null;
        }

        // ---- Drag & drop reordering ----

        private System.Windows.Point _dragStart;
        private LabelFieldRow? _dragCandidate;

        // Pressing the grip only arms the drag; movement is tracked on the whole list so a quick
        // drag that leaves the narrow grip before crossing the drag threshold still starts.
        private void FieldGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(LabelFieldList);
            _dragCandidate = (sender as FrameworkElement)?.DataContext as LabelFieldRow;
        }

        private void LabelFieldList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
            _dragCandidate = null;

        private void LabelFieldList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var delta = e.GetPosition(LabelFieldList) - _dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var row = _dragCandidate;
            _dragCandidate = null;
            row.IsDragging = true;
            try
            {
                // Blocks until the drop (or cancel); DragOver/Drop below do the work meanwhile.
                System.Windows.DragDrop.DoDragDrop(LabelFieldList,
                    new System.Windows.DataObject(typeof(LabelFieldRow), row), System.Windows.DragDropEffects.Move);
            }
            finally
            {
                row.IsDragging = false;
                FieldDropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void LabelFieldList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;
            if (!e.Data.GetDataPresent(typeof(LabelFieldRow)))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            ShowDropIndicator(GetDropIndex(e));
        }

        private void LabelFieldList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            FieldDropIndicator.Visibility = Visibility.Collapsed;
            if (e.Data.GetData(typeof(LabelFieldRow)) is not LabelFieldRow row)
                return;

            e.Handled = true;
            var from = _labelFieldRows.IndexOf(row);
            var to = GetDropIndex(e);
            // The insertion index counts the dragged row itself; removing it first shifts later slots up by one.
            if (to > from)
                to--;
            if (from < 0 || to == from)
                return;

            _labelFieldRows.Move(from, to);
        }

        private void LabelFieldList_DragLeave(object sender, System.Windows.DragEventArgs e) =>
            FieldDropIndicator.Visibility = Visibility.Collapsed;

        // Scrolls the Settings tab while dragging near its top/bottom edge, since the list is taller than the viewport.
        private void SettingsScroll_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            const double edge = 36;
            var y = e.GetPosition(SettingsScroll).Y;
            if (y < edge)
                SettingsScroll.LineUp();
            else if (y > SettingsScroll.ActualHeight - edge)
                SettingsScroll.LineDown();
        }

        /// <summary>Insertion slot under the mouse: before the first row whose upper half is below it, else the end.</summary>
        private int GetDropIndex(System.Windows.DragEventArgs e)
        {
            for (int i = 0; i < _labelFieldRows.Count; i++)
            {
                if (LabelFieldList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container
                    && e.GetPosition(container).Y < container.ActualHeight / 2)
                    return i;
            }
            return _labelFieldRows.Count;
        }

        private void ShowDropIndicator(int index)
        {
            // Rows are 8px apart (bottom margin); centre the line in that gap.
            const double gapCentre = 4;
            double y;
            if (index < _labelFieldRows.Count
                && LabelFieldList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement next)
            {
                y = next.TranslatePoint(new System.Windows.Point(0, 0), LabelFieldList).Y - gapCentre;
            }
            else if (LabelFieldList.ItemContainerGenerator.ContainerFromIndex(_labelFieldRows.Count - 1) is FrameworkElement last)
            {
                y = last.TranslatePoint(new System.Windows.Point(0, last.ActualHeight), LabelFieldList).Y - gapCentre;
            }
            else
            {
                return;
            }

            System.Windows.Controls.Canvas.SetTop(FieldDropIndicator, y - FieldDropIndicator.Height / 2);
            FieldDropIndicator.Width = LabelFieldList.ActualWidth;
            FieldDropIndicator.Visibility = Visibility.Visible;
        }

        private void TxtFileNamePattern_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateFileNameExample();
            UpdateUnsavedState();
        }

        // Previews the pattern being typed (not the saved one), with the chosen source file if any.
        private void UpdateFileNameExample()
        {
            var source = File.Exists(TxtSourceFile.Text) ? TxtSourceFile.Text : "DanhSachSanPham.xlsx";
            var fileName = ExportFileNameFormatter.Format(TxtFileNamePattern.Text, source, DateTime.Now);
            TxtFileNameExample.Text = string.Format(LocalizationManager.GetString("Str_FileNameExampleFormat"), fileName);
        }
    }
}
