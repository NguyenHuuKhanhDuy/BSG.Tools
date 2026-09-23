using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BSG.Tools.Models;
using ClosedXML.Excel;

namespace BSG.Tools.Services
{
    public class ProductRow
    {
        public string Name { get; set; } = "";        // TÊN SẢN PHẨM
        public string Origin { get; set; } = "";       // XUẤT XỨ
        public string Ingredients { get; set; } = "";  // THÀNH PHẦN
        public string Quantity { get; set; } = "";     // SL
    }

    /// <summary>
    /// Everything the export needs besides the source file itself:
    /// the checkbox-controlled supplier fields, and the always-on fields
    /// that come from the Settings tab.
    /// </summary>
    public class BuildOptions
    {
        public bool IncludeSupplier { get; set; }
        public string SupplierName { get; set; } = "";
        public string SupplierAddress { get; set; } = "";

        public string Importer { get; set; } = "";
        public string ImporterAddress { get; set; } = "";
        public string ProductionYear { get; set; } = "";

        public string UsageInstructions { get; set; } = "";
        public string StorageInstructions { get; set; } = "";

        /// <summary>Which content lines to write, in order, with their labels. Null means the defaults.</summary>
        public IReadOnlyList<LabelFieldSetting>? LabelFields { get; set; }
    }

    /// <summary>One "Label: value" line in a label's content block.</summary>
    public class LabelLine
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    }

    /// <summary>
    /// One product's label exactly as it will be written to the sheet - the
    /// shared data behind both the in-app preview and the Excel export, so
    /// the two can never disagree about what a label contains.
    /// </summary>
    public class LabelEntry
    {
        public int Stt { get; set; }
        public string Title { get; set; } = "";     // product name, already upper-cased
        public string Quantity { get; set; } = "";
        public IReadOnlyList<LabelLine> Lines { get; set; } = Array.Empty<LabelLine>();
    }

    public static class LabelExcelBuilder
    {
        private const string FontName = "Arial";

        // Column layout: A = STT, B..F = content (merged), G = SL
        private const int SttCol = 1;
        private const int ContentStartCol = 2;
        private const int ContentEndCol = 6;
        private const int SlCol = 7;

        /// <summary>
        /// Reads the source workbook (must have header row with columns
        /// "TÊN SẢN PHẨM", "XUẤT XỨ", "THÀNH PHẦN", "SL") and writes the
        /// label sheet to outputPath.
        /// </summary>
        public static void Build(string sourcePath, string outputPath, BuildOptions opts)
            => Build(LoadLabels(sourcePath, opts), outputPath);

        /// <summary>
        /// Reads the source workbook and builds every product's label content
        /// (STT, title, quantity and ordered "Label: value" lines), without
        /// writing anything. Throws if the source has no products.
        /// </summary>
        public static IReadOnlyList<LabelEntry> LoadLabels(string sourcePath, BuildOptions opts)
        {
            var products = ReadProducts(sourcePath);
            if (products.Count == 0)
                throw new InvalidOperationException("Không tìm thấy sản phẩm nào trong file nguồn (kiểm tra lại cột TÊN SẢN PHẨM).");

            return products
                .Select((p, i) => new LabelEntry
                {
                    Stt = i + 1,
                    Title = p.Name.ToUpperInvariant(),
                    Quantity = p.Quantity,
                    Lines = BuildContentLines(p, opts),
                })
                .ToList();
        }

        /// <summary>Writes already-built labels (see <see cref="LoadLabels"/>) to outputPath.</summary>
        public static void Build(IReadOnlyList<LabelEntry> labels, string outputPath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Nhãn sản phẩm");

            ws.Column(SttCol).Width = 6;
            for (int c = ContentStartCol; c <= ContentEndCol; c++)
                ws.Column(c).Width = 16;
            ws.Column(SlCol).Width = 8;

            double mergedContentColumnWidth = 0;
            for (int c = ContentStartCol; c <= ContentEndCol; c++)
                mergedContentColumnWidth += ws.Column(c).Width;

            // Page setup: fit to one page wide so Excel doesn't draw an
            // automatic page-break line through the middle of the table.
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Left = 0.3;
            ws.PageSetup.Margins.Right = 0.3;
            ws.PageSetup.Margins.Top = 0.5;
            ws.PageSetup.Margins.Bottom = 0.5;

            // ---- Header row ----
            const int headerRow = 1;
            ws.Cell(headerRow, SttCol).Value = "STT";
            var headerContentRange = ws.Range(headerRow, ContentStartCol, headerRow, ContentEndCol).Merge();
            ws.Cell(headerRow, ContentStartCol).Value = "THÔNG TIN SẢN PHẨM";
            ws.Cell(headerRow, SlCol).Value = "SỐ LƯỢNG";

            foreach (var rng in new[]
            {
                ws.Range(headerRow, SttCol, headerRow, SttCol),
                headerContentRange,
                ws.Range(headerRow, SlCol, headerRow, SlCol),
            })
            {
                // Style applied to the RANGE (not a single cell) so every cell
                // covered by a merge ends up with the same format. Styling only
                // the anchor cell leaves the other cells in the merge on the
                // default format, which is what made Excel flag the merged
                // block with an "inconsistent region" warning triangle.
                rng.Style.Font.FontName = FontName;
                rng.Style.Font.Bold = true;
                rng.Style.Font.FontSize = 12;
                rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            ws.Row(headerRow).Height = 22;
            var headerFullRow = ws.Range(headerRow, SttCol, headerRow, SlCol);
            headerFullRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            headerFullRow.Style.Border.BottomBorderColor = XLColor.Black;

            // ---- Product blocks ----
            // Start one row below the header so there is a blank spacer row,
            // matching the spacing already used between product blocks.
            int currentRow = headerRow + 2;

            foreach (var label in labels)
            {
                int titleRow = currentRow;
                int contentRow = currentRow + 1;

                var sttRange = ws.Range(titleRow, SttCol, contentRow, SttCol).Merge();
                ws.Cell(titleRow, SttCol).Value = label.Stt;
                sttRange.Style.Font.FontName = FontName;
                sttRange.Style.Font.Bold = true;
                sttRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sttRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                var titleRange = ws.Range(titleRow, ContentStartCol, titleRow, ContentEndCol).Merge();
                var titleCell = ws.Cell(titleRow, ContentStartCol);
                titleCell.Value = label.Title;
                titleRange.Style.Font.FontName = FontName;
                titleRange.Style.Font.Bold = true;
                titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                titleRange.Style.Alignment.WrapText = true;
                titleRange.Style.Fill.BackgroundColor = XLColor.White; // hides the seam gridline

                var contentRange = ws.Range(contentRow, ContentStartCol, contentRow, ContentEndCol).Merge();
                var contentCell = ws.Cell(contentRow, ContentStartCol);
                contentRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                contentRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                contentRange.Style.Alignment.WrapText = true;
                contentRange.Style.Fill.BackgroundColor = XLColor.White; // hides the seam gridline
                FillContentRichText(contentCell, label.Lines);

                var slRange = ws.Range(titleRow, SlCol, contentRow, SlCol).Merge();
                ws.Cell(titleRow, SlCol).Value = label.Quantity;
                slRange.Style.Font.FontName = FontName;
                slRange.Style.Font.Bold = true;
                slRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                slRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Borders: only the content block gets a box (title: top/left/right,
                // content: bottom/left/right - no border on the shared edge, so it
                // reads as one seamless block). STT and SL stay borderless.
                titleRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                titleRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                titleRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;

                contentRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                contentRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
                contentRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;

                ws.Row(titleRow).Height = 20;
                ws.Row(contentRow).Height = EstimateContentRowHeight(label.Lines, mergedContentColumnWidth);

                currentRow = contentRow + 2; // leaves one blank row between entries
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            workbook.SaveAs(outputPath);
        }

        /// <summary>
        /// Writes a blank source-file template: the header row expected by
        /// <see cref="ReadProducts"/> plus one example data row, so users know
        /// which columns to fill in.
        /// </summary>
        public static void BuildTemplate(string outputPath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Sheet1");

            ws.Column(1).Width = 30;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 45;
            ws.Column(4).Width = 10;

            var header = new[] { "TÊN SẢN PHẨM", "XUẤT XỨ", "THÀNH PHẦN", "SL" };
            for (int c = 0; c < header.Length; c++)
                ws.Cell(1, c + 1).Value = header[c];

            var headerRange = ws.Range(1, 1, 1, header.Length);
            headerRange.Style.Font.FontName = FontName;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F5");
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Cell(2, 1).Value = "Ví dụ: Kẹo dẻo trái cây";
            ws.Cell(2, 2).Value = "Việt Nam";
            ws.Cell(2, 3).Value = "Đường, hương liệu tự nhiên, chất tạo màu";
            ws.Cell(2, 4).Value = "100";

            var exampleRange = ws.Range(2, 1, 2, header.Length);
            exampleRange.Style.Font.FontName = FontName;
            exampleRange.Style.Font.FontColor = XLColor.FromHtml("#6B7280");
            exampleRange.Style.Font.Italic = true;

            var noteCell = ws.Cell(3, 1);
            noteCell.Value = "* Dòng trên chỉ là ví dụ - hãy thay bằng dữ liệu thật hoặc xoá đi trước khi dùng làm file nguồn.";
            noteCell.Style.Font.FontName = FontName;
            noteCell.Style.Font.Italic = true;
            noteCell.Style.Font.FontColor = XLColor.FromHtml("#6B7280");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            workbook.SaveAs(outputPath);
        }

        /// <summary>
        /// Builds the ordered (Label, Value) fields shown in a product's content
        /// cell, shared between rich-text rendering and row-height estimation
        /// so both always agree on what will actually be displayed.
        /// </summary>
        private static List<LabelLine> BuildContentLines(ProductRow p, BuildOptions opts)
        {
            var year = string.IsNullOrWhiteSpace(opts.ProductionYear)
                ? DateTime.Now.Year.ToString()
                : opts.ProductionYear;

            var lines = new List<LabelLine>();
            foreach (var field in LabelFieldCatalog.Normalize(opts.LabelFields))
            {
                if (!field.Enabled)
                    continue;

                // Supplier lines also depend on the "include supplier" checkbox on the export tab.
                bool isSupplierField = field.Key is LabelFieldCatalog.SupplierName or LabelFieldCatalog.SupplierAddress;
                if (isSupplierField && !opts.IncludeSupplier)
                    continue;

                var value = field.Key switch
                {
                    LabelFieldCatalog.SupplierName => opts.SupplierName,
                    LabelFieldCatalog.SupplierAddress => opts.SupplierAddress,
                    LabelFieldCatalog.Importer => opts.Importer,
                    LabelFieldCatalog.ImporterAddress => opts.ImporterAddress,
                    LabelFieldCatalog.ProductName => p.Name,
                    LabelFieldCatalog.Origin => p.Origin,
                    LabelFieldCatalog.Ingredients => p.Ingredients,
                    LabelFieldCatalog.UsageInstructions => opts.UsageInstructions,
                    LabelFieldCatalog.StorageInstructions => opts.StorageInstructions,
                    LabelFieldCatalog.ProductionYear => year,
                    _ => ""
                };

                var label = string.IsNullOrWhiteSpace(field.Label)
                    ? LabelFieldCatalog.DefaultLabel(field.Key)
                    : field.Label.Trim();
                lines.Add(new LabelLine { Label = label, Value = value });
            }

            return lines;
        }

        // Rough characters-per-Excel-column-width-unit for Arial 11 with wrap
        // text, used only to estimate how many display lines a field needs.
        private const double CharsPerWidthUnit = 1.0;
        private const double LineHeightPoints = 15.0;
        private const double ContentRowVerticalPadding = 6.0;
        private const double MinContentRowHeight = 40.0;

        /// <summary>
        /// Estimates a content row height (in points) from the number of
        /// fields and their text length, so short products (few/short fields)
        /// get a shorter row and long ones get a taller row - instead of a
        /// single fixed height for every product. This is an approximation:
        /// ClosedXML has no reliable auto-fit for wrapped merged cells, and
        /// exact pixel-perfect wrapping depends on the fonts installed on the
        /// machine that later opens the file in Excel.
        /// </summary>
        private static double EstimateContentRowHeight(IReadOnlyList<LabelLine> lines, double mergedColumnWidthUnits)
        {
            double charsPerLine = Math.Max(1, mergedColumnWidthUnits * CharsPerWidthUnit);

            int totalWrappedLines = 0;
            foreach (var line in lines)
            {
                int displayLength = line.Label.Length + 2 + line.Value.Length; // "Label: value"
                totalWrappedLines += Math.Max(1, (int)Math.Ceiling(displayLength / charsPerLine));
            }

            double height = totalWrappedLines * LineHeightPoints + ContentRowVerticalPadding;
            return Math.Max(MinContentRowHeight, height);
        }

        /// <summary>
        /// Builds the multi-line rich text for the content cell: each field is
        /// its own line ("Label: value"), label bold, value normal. Fields with
        /// no value only get the bold label (no trailing empty run - an empty
        /// inline-string run has been observed to make real Excel show a
        /// "we found a problem with some content" repair prompt).
        /// </summary>
        private static void FillContentRichText(IXLCell cell, IReadOnlyList<LabelLine> lines)
        {
            var richText = cell.GetRichText();
            for (int i = 0; i < lines.Count; i++)
            {
                var label = lines[i].Label;
                var value = lines[i].Value;
                var prefix = i == 0 ? "" : "\n";

                var labelRun = richText.AddText($"{prefix}{label}: ");
                labelRun.FontName = FontName;
                labelRun.Bold = true;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    var valueRun = richText.AddText(value);
                    valueRun.FontName = FontName;
                    valueRun.Bold = false;
                }
            }
        }

        /// <summary>
        /// Reads products from the first worksheet of the source file. Column
        /// positions are looked up by header name in row 1, so column order in
        /// the source file doesn't matter. Rows with no product name are skipped.
        /// </summary>
        private static List<ProductRow> ReadProducts(string sourcePath)
        {
            using var workbook = new XLWorkbook(sourcePath);
            var ws = workbook.Worksheet(1);
            var headerRow = ws.Row(1);

            int? nameCol = null, originCol = null, ingredientsCol = null, qtyCol = null;

            foreach (var cell in headerRow.CellsUsed())
            {
                var text = cell.GetString().Trim();
                switch (text)
                {
                    case "TÊN SẢN PHẨM": nameCol = cell.Address.ColumnNumber; break;
                    case "XUẤT XỨ": originCol = cell.Address.ColumnNumber; break;
                    case "THÀNH PHẦN": ingredientsCol = cell.Address.ColumnNumber; break;
                    case "SL": qtyCol = cell.Address.ColumnNumber; break;
                }
            }

            if (nameCol is null)
                throw new InvalidOperationException("Không tìm thấy cột 'TÊN SẢN PHẨM' trong file nguồn.");

            var products = new List<ProductRow>();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                var nameCell = ws.Cell(r, nameCol.Value);
                var name = nameCell.GetString().Trim();
                if (string.IsNullOrEmpty(name))
                    continue; // skip placeholder/empty rows

                products.Add(new ProductRow
                {
                    Name = name,
                    Origin = originCol is null ? "" : ws.Cell(r, originCol.Value).GetString().Trim(),
                    Ingredients = ingredientsCol is null ? "" : ws.Cell(r, ingredientsCol.Value).GetString().Trim(),
                    Quantity = qtyCol is null ? "" : ws.Cell(r, qtyCol.Value).GetString().Trim(),
                });
            }

            return products;
        }
    }
}
