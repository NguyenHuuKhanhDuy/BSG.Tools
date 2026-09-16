using System;
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
            LoadSettingsIntoUi();
            UpdateSupplierInputsEnabled();
            UpdateExportButtonEnabled();
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
            TxtUpdateBanner.Text = $"Có bản cập nhật mới: v{update.TargetFullRelease.Version} — bấm \"Cập nhật ngay\" để cài đặt.";
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate is null)
                return;

            BtnUpdateNow.IsEnabled = false;
            BtnUpdateNow.Content = "Đang cập nhật...";

            var success = await UpdateService.DownloadAndApplyUpdateAsync(_pendingUpdate);

            if (!success)
            {
                BtnUpdateNow.IsEnabled = true;
                BtnUpdateNow.Content = "Cập nhật ngay";
                TxtUpdateBanner.Text = "Cập nhật thất bại. Vui lòng kiểm tra kết nối mạng và thử lại.";
            }
            // Nếu thành công, ApplyUpdatesAndRestart() đã tự khởi động lại app — code sau điểm này sẽ không chạy.
        }

        // ---------------- Xuất file tab ----------------

        private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file Excel nguồn",
                Filter = "Excel files (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtSourceFile.Text = dialog.FileName;
                UpdateExportButtonEnabled();
            }
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Lưu file mẫu",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = "Mau_du_lieu_san_pham.xlsx"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                LabelExcelBuilder.BuildTemplate(dialog.FileName);
                _lastSuccessfulOutputPath = dialog.FileName;
                TxtStatus.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BrushStatusSuccess"];
                TxtStatus.Text = $"Đã tải file mẫu: {dialog.FileName} (nhấp để mở thư mục)";
                TxtStatus.Cursor = System.Windows.Input.Cursors.Hand;
                TxtStatus.TextDecorations = TextDecorations.Underline;
            }
            catch (Exception ex)
            {
                SetStatusError($"Lỗi khi tạo file mẫu: {ex.Message}");
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

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSourceFile.Text) || !File.Exists(TxtSourceFile.Text))
            {
                SetStatusError("Vui lòng chọn file Excel nguồn hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtOutputFolder.Text) || !Directory.Exists(TxtOutputFolder.Text))
            {
                SetStatusError("Vui lòng chọn thư mục lưu file xuất.");
                return;
            }

            var settings = SettingsService.Load();

            var options = new BuildOptions
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

            var sourceName = Path.GetFileNameWithoutExtension(TxtSourceFile.Text);
            var dateSuffix = DateTime.Now.ToString("ddMMyyyy");
            var outputPath = Path.Combine(TxtOutputFolder.Text, $"temphu_{sourceName}_{dateSuffix}.xlsx");

            try
            {
                LabelExcelBuilder.Build(TxtSourceFile.Text, outputPath, options);
                _lastSuccessfulOutputPath = outputPath;
                ClearStatus();
                ShowToast("Xuất file thành công.", showOpenFolder: true);
            }
            catch (Exception ex)
            {
                SetStatusError($"Lỗi khi xuất file: {ex.Message}");
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
                SetStatusError("Không thể mở thư mục chứa file (file hoặc thư mục có thể đã bị xoá/đổi tên).");
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
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
            {
                Importer = TxtImporter.Text.Trim(),
                ImporterAddress = TxtImporterAddress.Text.Trim(),
                ProductionYear = TxtProductionYear.Text.Trim(),
                UsageInstructions = TxtUsageInstructions.Text.Trim(),
                StorageInstructions = TxtStorageInstructions.Text.Trim()
            };

            SettingsService.Save(settings);
            _lastSuccessfulOutputPath = null;
            ClearStatus();
            ShowToast("Đã lưu cài đặt.", showOpenFolder: false);
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
                "Thao tác này sẽ xoá toàn bộ cài đặt đã lưu và đưa các trường về giá trị mặc định. Bạn có chắc chắn muốn tiếp tục?",
                "Khôi phục mặc định",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var defaults = new AppSettings();
            SettingsService.Save(defaults);

            TxtImporter.Text = defaults.Importer;
            TxtImporterAddress.Text = defaults.ImporterAddress;
            TxtProductionYear.Text = DateTime.Now.Year.ToString();
            TxtUsageInstructions.Text = defaults.UsageInstructions;
            TxtStorageInstructions.Text = defaults.StorageInstructions;

            ShowToast("Đã khôi phục mặc định.", showOpenFolder: false);
        }
    }
}
