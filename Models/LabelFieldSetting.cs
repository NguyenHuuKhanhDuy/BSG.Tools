using System.Collections.Generic;
using System.Linq;

namespace BSG.Tools.Models
{
    /// <summary>One configurable line of a label's content block ("Label: value").</summary>
    public class LabelFieldSetting
    {
        /// <summary>One of the <see cref="LabelFieldCatalog"/> keys; stored as a string so settings.json stays readable.</summary>
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Fixed set of label fields with their default order and labels. Labels are printed label
    /// content (Vietnamese), not UI strings, so they aren't localized.
    /// </summary>
    public static class LabelFieldCatalog
    {
        public const string SupplierName = "SupplierName";
        public const string SupplierAddress = "SupplierAddress";
        public const string Importer = "Importer";
        public const string ImporterAddress = "ImporterAddress";
        public const string ProductName = "ProductName";
        public const string Origin = "Origin";
        public const string Ingredients = "Ingredients";
        public const string UsageInstructions = "UsageInstructions";
        public const string StorageInstructions = "StorageInstructions";
        public const string ProductionYear = "ProductionYear";

        private static readonly (string Key, string Label)[] DefaultFields =
        {
            (SupplierName, "Nhà cung cấp"),
            (SupplierAddress, "Địa chỉ nhà Cung cấp"),
            (Importer, "Nhập khẩu và phân phối"),
            (ImporterAddress, "Địa chỉ nhà nhập khẩu"),
            (ProductName, "Tên Sản phẩm"),
            (Origin, "Xuất xứ"),
            (Ingredients, "Thành phần"),
            (UsageInstructions, "Hướng dẫn sử dụng"),
            (StorageInstructions, "Cách bảo quản"),
            (ProductionYear, "Năm sản xuất"),
        };

        public static string DefaultLabel(string key) =>
            DefaultFields.FirstOrDefault(f => f.Key == key).Label ?? key;

        public static List<LabelFieldSetting> Defaults() =>
            DefaultFields.Select(f => new LabelFieldSetting { Key = f.Key, Label = f.Label }).ToList();

        /// <summary>
        /// Every known field exactly once: saved order kept, unknown/duplicate keys dropped, missing
        /// fields (older settings file, newer app) appended enabled with their default label.
        /// </summary>
        public static List<LabelFieldSetting> Normalize(IEnumerable<LabelFieldSetting>? saved)
        {
            var result = new List<LabelFieldSetting>();
            foreach (var field in saved ?? Enumerable.Empty<LabelFieldSetting>())
            {
                if (DefaultFields.Any(f => f.Key == field.Key) && result.All(r => r.Key != field.Key))
                    result.Add(new LabelFieldSetting { Key = field.Key, Label = field.Label ?? "", Enabled = field.Enabled });
            }

            foreach (var (key, label) in DefaultFields)
            {
                if (result.All(r => r.Key != key))
                    result.Add(new LabelFieldSetting { Key = key, Label = label });
            }

            return result;
        }
    }
}
