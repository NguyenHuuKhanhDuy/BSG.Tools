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
    }
}
