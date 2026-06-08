using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace tiktok_Omni.Services.Mascot
{
    public static class MascotExcelHelper
    {
        public static string GetScriptsRoot(string storageRoot)
        {
            var root = string.IsNullOrWhiteSpace(storageRoot)
                ? ProfileScopedPaths.ResolveStorageRoot()
                : storageRoot.Trim();
            var dir = Path.Combine(root, "Generated", "MascotScripts");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string BuildExcelFileName(string profile, string requestHint)
        {
            var nick = ProfileScopedPaths.ResolveProfileName(profile);
            var slug = Slugify(requestHint);
            if (string.IsNullOrWhiteSpace(slug)) slug = "story";
            return nick + "_Mascot_" + slug + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx";
        }

        public static void ExportScenes(string excelPath, IList<MascotScene> scenes)
        {
            if (scenes == null || scenes.Count == 0)
                throw new InvalidOperationException("Khong co canh de xuat Excel.");
            var dir = Path.GetDirectoryName(Path.GetFullPath(excelPath));
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Scenes");
                ws.Cell(1, 1).Value = "Thu tu";
                ws.Cell(1, 2).Value = "Kich ban";
                ws.Cell(1, 3).Value = "Prompt Video";
                ws.Row(1).Style.Font.Bold = true;
                var row = 2;
                foreach (var scene in scenes.OrderBy(s => s.Order))
                {
                    ws.Cell(row, 1).Value = "Scene " + scene.Order;
                    ws.Cell(row, 2).Value = scene.Voiceover ?? string.Empty;
                    ws.Cell(row, 3).Value = scene.ImagePrompt ?? string.Empty;
                    row++;
                }
                ws.Columns().AdjustToContents();
                wb.SaveAs(excelPath);
            }
        }

        public static List<MascotScene> ReadScenes(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
                throw new FileNotFoundException("File Excel khong ton tai.", excelPath);

            var list = new List<MascotScene>();
            using (var wb = new XLWorkbook(excelPath))
            {
                var ws = wb.Worksheets.First();
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (var r = 2; r <= lastRow; r++)
                {
                    var orderText = ws.Cell(r, 1).GetString().Trim();
                    var voice = ws.Cell(r, 2).GetString().Trim();
                    var prompt = ws.Cell(r, 3).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(voice) && string.IsNullOrWhiteSpace(prompt)) continue;
                    list.Add(new MascotScene
                    {
                        Order = ParseSceneOrder(orderText, list.Count + 1),
                        Voiceover = voice,
                        ImagePrompt = prompt
                    });
                }
            }
            if (list.Count == 0)
                throw new InvalidOperationException("Excel khong co dong canh hop le.");
            return list;
        }

        private static int ParseSceneOrder(string orderText, int fallback)
        {
            if (string.IsNullOrWhiteSpace(orderText)) return fallback;
            var digits = new string(orderText.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                return n;
            return fallback;
        }

        private static string Slugify(string text)
        {
            var s = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Length > 40) s = s.Substring(0, 40);
            return new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        }
    }
}