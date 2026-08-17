using System;

using System.Collections.Generic;

using System.Globalization;

using System.IO;

using System.Linq;

using ClosedXML.Excel;



namespace tiktok_Omni.Services.Showcase

{

    /// <summary>Xuất/nhập Excel prompt clip cho Showcase — mỗi dòng là 1 phân cảnh.</summary>

    public static class ShowcaseExcelHelper

    {

        public const string ClipPromptsSheetName = "Clip Prompts";

        private const string LegacyVeoSheetName = "Veo Prompts";



        public static string BuildExcelFileName(string profile, string productName)

        {

            var nick = ProfileScopedPaths.ResolveProfileName(profile);

            var slug = Slugify(productName);

            if (string.IsNullOrWhiteSpace(slug))

            {

                slug = "showcase";

            }



            return nick + "_Showcase_" + slug + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx";

        }



        public static string Export(

            string excelPath,

            string theme,

            string hookText,

            string ctaText,

            IList<AiVideoGenInputItem> orderedScenes)

        {

            if (orderedScenes == null || orderedScenes.Count == 0)

            {

                throw new InvalidOperationException("Không có cảnh nào để xuất Excel.");

            }



            var dir = Path.GetDirectoryName(Path.GetFullPath(excelPath));

            if (!string.IsNullOrWhiteSpace(dir))

            {

                Directory.CreateDirectory(dir);

            }



            using (var wb = new XLWorkbook())

            {

                var clipWs = wb.Worksheets.Add(ClipPromptsSheetName);

                clipWs.Cell(1, 1).Value = "Thu tu";

                clipWs.Cell(1, 2).Value = "Anh nguon";

                clipWs.Cell(1, 3).Value = "Loai anh";

                clipWs.Cell(1, 4).Value = "Cong cu clip";

                clipWs.Cell(1, 5).Value = "Prompt Veo";

                clipWs.Cell(1, 6).Value = "Prompt Kling";

                clipWs.Cell(1, 7).Value = "Zoom goi y";

                clipWs.Cell(1, 8).Value = "Ten file clip can dat";

                clipWs.Cell(1, 9).Value = "Ten canh";

                clipWs.Cell(1, 10).Value = "Vai tro";

                clipWs.Row(1).Style.Font.Bold = true;



                var row = 2;

                for (var i = 0; i < orderedScenes.Count; i++)

                {

                    var scene = orderedScenes[i];

                    var order = i + 1;

                    clipWs.Cell(row, 1).Value = "Scene " + order;

                    clipWs.Cell(row, 2).Value = string.IsNullOrWhiteSpace(scene?.ThumbnailPath)

                        ? (scene?.ImageUrl ?? string.Empty)

                        : scene.ThumbnailPath;

                    clipWs.Cell(row, 3).Value = ShowcaseClipToolHelper.GetImageKindDisplayLabel(scene?.ShowcaseImageKind);

                    clipWs.Cell(row, 4).Value = ShowcaseClipToolHelper.GetToolDisplayLabel(scene?.ShowcaseClipTool);

                    clipWs.Cell(row, 5).Value = scene?.VeoPrompt ?? string.Empty;

                    clipWs.Cell(row, 6).Value = scene?.KlingPrompt ?? string.Empty;

                    clipWs.Cell(row, 7).Value = scene?.ZoomHint ?? string.Empty;

                    clipWs.Cell(row, 8).Value = "scene_" + order.ToString("D2", CultureInfo.InvariantCulture) + ".mp4";

                    clipWs.Cell(row, 9).Value = scene?.SceneTitle ?? string.Empty;

                    clipWs.Cell(row, 10).Value = scene?.SceneRole ?? string.Empty;

                    row++;

                }



                clipWs.Column(1).Width = 12;

                clipWs.Column(2).Width = 48;

                clipWs.Column(3).Width = 12;

                clipWs.Column(4).Width = 12;

                clipWs.Column(5).Width = 72;

                clipWs.Column(6).Width = 72;

                clipWs.Column(7).Width = 28;

                clipWs.Column(8).Width = 22;

                clipWs.Column(9).Width = 24;

                clipWs.Column(10).Width = 14;

                clipWs.Cell(row + 1, 1).Value =

                    "Huong dan: Veo/Kling — copy prompt + anh sang cong cu I2V. Zoom — bam «Tao clip Zoom» trong app hoac tu tao. Dat ten clip dung cot «Ten file clip can dat», bo vao clips_render, roi Render.";



                var voiceWs = wb.Worksheets.Add("Voiceover (app)");

                voiceWs.Cell(1, 1).Value = "Muc";

                voiceWs.Cell(1, 2).Value = "Noi dung (tieng Viet — dung khi Render, KHONG dung cho clip I2V)";

                voiceWs.Row(1).Style.Font.Bold = true;

                voiceWs.Cell(2, 1).Value = "Chu de";

                voiceWs.Cell(2, 2).Value = theme ?? string.Empty;

                voiceWs.Cell(3, 1).Value = "Hook (mo dau)";

                voiceWs.Cell(3, 2).Value = hookText ?? string.Empty;

                voiceWs.Cell(4, 1).Value = "CTA (ket thuc)";

                voiceWs.Cell(4, 2).Value = ctaText ?? string.Empty;

                voiceWs.Column(1).Width = 18;

                voiceWs.Column(2).Width = 90;



                var voiceRow = 6;

                voiceWs.Cell(voiceRow, 1).Value = "Canh";

                voiceWs.Cell(voiceRow, 2).Value = "Kich ban (Voiceover)";

                voiceWs.Row(voiceRow).Style.Font.Bold = true;

                voiceRow++;

                for (var i = 0; i < orderedScenes.Count; i++)

                {

                    var scene = orderedScenes[i];

                    voiceWs.Cell(voiceRow, 1).Value = "Scene " + (i + 1);

                    voiceWs.Cell(voiceRow, 2).Value = scene?.SceneVoiceover ?? string.Empty;

                    voiceRow++;

                }



                return SaveWorkbookSafely(wb, excelPath);

            }

        }



        private static string SaveWorkbookSafely(XLWorkbook workbook, string excelPath)

        {

            var fullPath = Path.GetFullPath(excelPath);

            var directory = Path.GetDirectoryName(fullPath) ?? ".";

            Directory.CreateDirectory(directory);



            var tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp.xlsx";

            try

            {

                workbook.SaveAs(tempPath);



                if (TryReplaceFile(tempPath, fullPath))

                {

                    return fullPath;

                }



                var alternatePath = BuildAlternateExcelPath(fullPath);

                if (TryReplaceFile(tempPath, alternatePath))

                {

                    return alternatePath;

                }



                throw new IOException(

                    "Không ghi được Excel — hãy đóng file .xlsx cũ trong Excel rồi thử lại.");

            }

            finally

            {

                TryDeleteIfExists(tempPath);

            }

        }



        private static bool TryReplaceFile(string sourcePath, string destinationPath)

        {

            for (var attempt = 1; attempt <= 4; attempt++)

            {

                try

                {

                    if (File.Exists(destinationPath))

                    {

                        File.Delete(destinationPath);

                    }



                    File.Move(sourcePath, destinationPath);

                    return true;

                }

                catch (IOException) when (attempt < 4)

                {

                    System.Threading.Thread.Sleep(120 * attempt);

                }

                catch (UnauthorizedAccessException) when (attempt < 4)

                {

                    System.Threading.Thread.Sleep(120 * attempt);

                }

            }



            return false;

        }



        private static string BuildAlternateExcelPath(string originalPath)

        {

            var directory = Path.GetDirectoryName(originalPath) ?? ".";

            var baseName = Path.GetFileNameWithoutExtension(originalPath);

            var extension = Path.GetExtension(originalPath);

            if (string.IsNullOrWhiteSpace(extension))

            {

                extension = ".xlsx";

            }



            for (var i = 0; i < 20; i++)

            {

                var suffix = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);

                if (i > 0)

                {

                    suffix += "_" + i.ToString(CultureInfo.InvariantCulture);

                }



                var candidate = Path.Combine(directory, baseName + "_export_" + suffix + extension);

                if (!File.Exists(candidate))

                {

                    return candidate;

                }

            }



            return Path.Combine(directory, baseName + "_export_" + Guid.NewGuid().ToString("N") + extension);

        }



        private static void TryDeleteIfExists(string path)

        {

            try

            {

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))

                {

                    File.Delete(path);

                }

            }

            catch

            {

                // ignored

            }

        }



        public static void ImportEdits(string excelPath, IList<AiVideoGenInputItem> orderedScenes)

        {

            if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))

            {

                throw new FileNotFoundException("File Excel không tồn tại.", excelPath);

            }



            if (orderedScenes == null || orderedScenes.Count == 0)

            {

                return;

            }



            using (var wb = new XLWorkbook(excelPath))

            {

                ImportClipPromptEdits(wb, orderedScenes);

                ImportVoiceoverEdits(wb, orderedScenes);

            }

        }



        private static void ImportClipPromptEdits(XLWorkbook wb, IList<AiVideoGenInputItem> orderedScenes)

        {

            IXLWorksheet ws = null;

            if (wb.Worksheets.Contains(ClipPromptsSheetName))

            {

                ws = wb.Worksheet(ClipPromptsSheetName);

                ImportModernClipSheet(ws, orderedScenes);

                return;

            }



            if (wb.Worksheets.Contains(LegacyVeoSheetName))

            {

                ws = wb.Worksheet(LegacyVeoSheetName);

                ImportLegacyVeoSheet(ws, orderedScenes);

                return;

            }



            if (wb.Worksheets.Contains("Scenes"))

            {

                ImportLegacyScenesSheet(wb.Worksheet("Scenes"), orderedScenes);

                return;

            }



            ws = wb.Worksheets.FirstOrDefault();

            if (ws == null)

            {

                return;

            }



            if (string.Equals(ws.Name, LegacyVeoSheetName, StringComparison.OrdinalIgnoreCase) ||

                ws.Cell(1, 3).GetString().IndexOf("Veo", StringComparison.OrdinalIgnoreCase) >= 0)

            {

                ImportLegacyVeoSheet(ws, orderedScenes);

            }

            else

            {

                ImportModernClipSheet(ws, orderedScenes);

            }

        }



        private static void ImportModernClipSheet(IXLWorksheet ws, IList<AiVideoGenInputItem> orderedScenes)

        {

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= lastRow; r++)

            {

                var order = ParseSceneOrder(ws.Cell(r, 1).GetString().Trim(), -1);

                if (order < 1 || order > orderedScenes.Count)

                {

                    continue;

                }



                var scene = orderedScenes[order - 1];

                if (scene == null)

                {

                    continue;

                }



                scene.ShowcaseImageKind = ParseImageKind(ws.Cell(r, 3).GetString().Trim(), scene.ShowcaseImageKind);

                scene.ShowcaseClipTool = ParseClipTool(ws.Cell(r, 4).GetString().Trim(), scene.ShowcaseClipTool);

                scene.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(ws.Cell(r, 5).GetString().Trim(), order - 1);

                scene.KlingPrompt = ShowcaseKlingPromptSanitizer.Sanitize(ws.Cell(r, 6).GetString().Trim(), order - 1);

                scene.ZoomHint = ws.Cell(r, 7).GetString().Trim();

                scene.SceneTitle = ws.Cell(r, 9).GetString().Trim();

                scene.SceneRole = ws.Cell(r, 10).GetString().Trim();

            }

        }



        private static void ImportLegacyVeoSheet(IXLWorksheet ws, IList<AiVideoGenInputItem> orderedScenes)

        {

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= lastRow; r++)

            {

                var order = ParseSceneOrder(ws.Cell(r, 1).GetString().Trim(), -1);

                if (order < 1 || order > orderedScenes.Count)

                {

                    continue;

                }



                var scene = orderedScenes[order - 1];

                if (scene == null)

                {

                    continue;

                }



                scene.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(ws.Cell(r, 3).GetString().Trim(), order - 1);

                scene.SceneTitle = ws.Cell(r, 5).GetString().Trim();

                scene.SceneRole = ws.Cell(r, 6).GetString().Trim();

            }

        }



        private static void ImportLegacyScenesSheet(IXLWorksheet ws, IList<AiVideoGenInputItem> orderedScenes)

        {

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 2; r <= lastRow; r++)

            {

                var order = ParseSceneOrder(ws.Cell(r, 1).GetString().Trim(), -1);

                if (order < 1 || order > orderedScenes.Count)

                {

                    continue;

                }



                var scene = orderedScenes[order - 1];

                if (scene == null)

                {

                    continue;

                }



                scene.SceneRole = ws.Cell(r, 2).GetString().Trim();

                scene.SceneTitle = ws.Cell(r, 3).GetString().Trim();

                scene.SceneVoiceover = ws.Cell(r, 5).GetString().Trim();

                scene.VeoPrompt = ShowcaseVeoPromptSanitizer.Sanitize(ws.Cell(r, 6).GetString().Trim(), order - 1);

            }

        }



        private static void ImportVoiceoverEdits(XLWorkbook wb, IList<AiVideoGenInputItem> orderedScenes)

        {

            if (!wb.Worksheets.Contains("Voiceover (app)"))

            {

                return;

            }



            var ws = wb.Worksheet("Voiceover (app)");

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (var r = 7; r <= lastRow; r++)

            {

                var orderText = ws.Cell(r, 1).GetString().Trim();

                var order = ParseSceneOrder(orderText, -1);

                if (order < 1 || order > orderedScenes.Count)

                {

                    continue;

                }



                var scene = orderedScenes[order - 1];

                if (scene == null)

                {

                    continue;

                }



                scene.SceneVoiceover = ws.Cell(r, 2).GetString().Trim();

            }

        }



        private static string ParseImageKind(string cellText, string fallback)

        {

            var text = (cellText ?? string.Empty).Trim();

            if (text.IndexOf("on", StringComparison.OrdinalIgnoreCase) >= 0 &&

                text.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0)

            {

                return ShowcaseClipToolHelper.KindOnModel;

            }



            if (text.IndexOf("flat", StringComparison.OrdinalIgnoreCase) >= 0)

            {

                return ShowcaseClipToolHelper.KindFlatlay;

            }



            return ShowcaseClipToolHelper.NormalizeImageKind(fallback);

        }



        private static string ParseClipTool(string cellText, string fallback)

        {

            var text = (cellText ?? string.Empty).Trim().ToLowerInvariant();

            if (text.Contains("kling"))

            {

                return ShowcaseClipToolHelper.ToolKling;

            }



            if (text.Contains("zoom"))

            {

                return ShowcaseClipToolHelper.ToolZoom;

            }



            if (text.Contains("veo"))

            {

                return ShowcaseClipToolHelper.ToolVeo;

            }



            return ShowcaseClipToolHelper.NormalizeClipTool(fallback);

        }



        private static int ParseSceneOrder(string orderText, int fallback)

        {

            if (string.IsNullOrWhiteSpace(orderText))

            {

                return fallback;

            }



            var digits = new string(orderText.Where(char.IsDigit).ToArray());

            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : fallback;

        }



        private static string Slugify(string text)

        {

            var s = (text ?? string.Empty).Trim().ToLowerInvariant();

            if (s.Length > 40)

            {

                s = s.Substring(0, 40);

            }



            return new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');

        }

    }

}


