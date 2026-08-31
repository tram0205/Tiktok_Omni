using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public static class ProductAdImagePromptExcelHelper
    {
        public static string GetDefaultFolder(string profileName, string storageRoot = null)
        {
            return ProfileScopedPaths.GetProductAdImageDirectory(storageRoot, profileName, create: true);
        }

        public static string BuildExcelFileName(string profileName, string productName)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profileName);
            var slug = ProductAdImagePromptBuilder.Slugify(productName);
            return nick + "_AnhQC_" + slug + "_"
                   + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                   + ".xlsx";
        }

        public static void ExportShots(string excelPath, IList<ProductAdImageShotPlan> shots)
        {
            if (shots == null || shots.Count == 0)
            {
                throw new InvalidOperationException("Chưa có prompt để xuất Excel.");
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(excelPath));
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Prompts");
                ws.Cell(1, 1).Value = "STT";
                ws.Cell(1, 2).Value = "Loại shot";
                ws.Cell(1, 3).Value = "Tiêu đề";
                ws.Cell(1, 4).Value = "Prompt (EN)";
                ws.Cell(1, 5).Value = "Tên file";
                ws.Cell(1, 6).Value = "Tỉ lệ";
                ws.Row(1).Style.Font.Bold = true;

                var row = 2;
                foreach (var shot in shots.Where(s => s != null).OrderBy(s => s.Index))
                {
                    ws.Cell(row, 1).Value = shot.Index > 0 ? shot.Index : row - 1;
                    ws.Cell(row, 2).Value = ProductAdImageShotPlan.FormatShotType(shot.ShotType);
                    ws.Cell(row, 3).Value = shot.Title ?? string.Empty;
                    ws.Cell(row, 4).Value = shot.Prompt ?? string.Empty;
                    ws.Cell(row, 5).Value = shot.OutputFileName ?? string.Empty;
                    ws.Cell(row, 6).Value = ProductAdImageAspectRatioHelper.GetDisplayLabel(shot.AspectRatio);
                    row++;
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(excelPath);
            }
        }
    }
}
