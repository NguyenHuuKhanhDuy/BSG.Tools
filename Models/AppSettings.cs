namespace BSG.Tools.Models
{
    /// <summary>
    /// Values configured in the "Cài đặt" tab. Persisted to disk so the user
    /// only has to fill them in once.
    /// </summary>
    public class AppSettings
    {
        public string Importer { get; set; } = "";           // Nhập khẩu và phân phối
        public string ImporterAddress { get; set; } = "";     // Địa chỉ nhà nhập khẩu

        /// <summary>
        /// Năm sản xuất. Null/empty means "use the current year at export time".
        /// If the user types a custom value, that exact value is kept and reused.
        /// </summary>
        public string? ProductionYear { get; set; }

        public string UsageInstructions { get; set; } = "";    // Hướng dẫn sử dụng
        public string StorageInstructions { get; set; } = "";  // Cách bảo quản

        /// <summary>Export file name pattern — see <see cref="Services.ExportFileNameFormatter"/>.</summary>
        public string ExportFileNamePattern { get; set; } = Services.ExportFileNameFormatter.DefaultPattern;

        /// <summary>
        /// Label content lines in display order. May be null or incomplete when read
        /// from an older settings file; pass it through <see cref="LabelFieldCatalog.Normalize"/>.
        /// </summary>
        public List<LabelFieldSetting>? LabelFields { get; set; }

        /// <summary>"Light" or "Dark" — see <see cref="Services.ThemeManager"/>.</summary>
        public string Theme { get; set; } = Services.ThemeManager.Light;

        /// <summary>"vi" or "en" — see <see cref="Services.LocalizationManager"/>.</summary>
        public string Language { get; set; } = Services.LocalizationManager.Vietnamese;
    }
}
