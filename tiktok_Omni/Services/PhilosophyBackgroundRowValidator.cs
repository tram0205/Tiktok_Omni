using System;
using System.IO;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Kiểm tra một dòng quote đủ tài nguyên nền để render/preview chưa.</summary>
    public static class PhilosophyBackgroundRowValidator
    {
        public sealed class RowValidationResult
        {
            public bool IsValid { get; set; }

            public string Summary { get; set; } = string.Empty;

            public string Detail { get; set; } = string.Empty;
        }

        public static RowValidationResult Validate(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            string profileName,
            AppSettings settings)
        {
            var result = new RowValidationResult();
            if (quote == null)
            {
                result.Summary = "!";
                result.Detail = "Thiếu dòng quote.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(quote.Content))
            {
                result.Summary = "!";
                result.Detail = "Thiếu nội dung quote.";
                return result;
            }

            var rowProfile = PhilosophyBatchHelper.ResolveBatchProfileName(batch, profileName);
            var visualMode = PhilosophyVisualModes.Normalize(quote.VisualMode);
            string error;

            if (visualMode == PhilosophyVisualModes.Broll)
            {
                if (!PhilosophyBRollSelection.TryValidateSelection(quote.BRollFolder, rowProfile, out error))
                {
                    result.Summary = "!";
                    result.Detail = error;
                    return result;
                }
            }
            else if (visualMode == PhilosophyVisualModes.ImageZoom
                     || visualMode == PhilosophyVisualModes.ImageSlideshow)
            {
                if (!PhilosophyBRollSelection.TryValidateZoomImages(quote.ZoomImagePaths, rowProfile, out error))
                {
                    result.Summary = "!";
                    result.Detail = error;
                    return result;
                }
            }
            else if (visualMode == PhilosophyVisualModes.ZoomBrollHybrid)
            {
                if (!PhilosophyBRollSelection.TryValidateZoomImages(quote.ZoomImagePaths, rowProfile, out error))
                {
                    result.Summary = "!";
                    result.Detail = error;
                    return result;
                }

                if (!PhilosophyBRollSelection.TryValidateSelection(quote.BRollFolder, rowProfile, out error))
                {
                    result.Summary = "!";
                    result.Detail = error + " (cần B-roll outro).";
                    return result;
                }
            }
            else if (visualMode == PhilosophyVisualModes.AiStillZoom)
            {
                if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
                {
                    result.Summary = "!";
                    result.Detail = "Cần AI API Key (Gemini) trong Cài đặt.";
                    return result;
                }

                var refPath = PhilosophyBatchHelper.ResolveReferenceImagePath(batch, quote);
                if (string.IsNullOrEmpty(refPath))
                {
                    var mascot = PhilosophyGeminiBackgroundContext.BuildMascotContext(rowProfile, null);
                    if (!mascot.HasMascotImage)
                    {
                        result.Summary = "!";
                        result.Detail = "Cần ảnh ref batch hoặc mascot profile.";
                        return result;
                    }
                }
            }
            else if (visualMode == PhilosophyVisualModes.PreRendered)
            {
                var folder = (quote.SceneVideoFolder ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    result.Summary = "!";
                    result.Detail = "Cần thư mục video phân cảnh.";
                    return result;
                }
            }
            else if (string.IsNullOrWhiteSpace(settings?.VeoApiKey) || string.IsNullOrWhiteSpace(settings?.VeoEndpoint))
            {
                result.Summary = "!";
                result.Detail = "Loại nền AI Veo cần Veo API Key + Endpoint.";
                return result;
            }

            result.IsValid = true;
            result.Summary = "✓";
            result.Detail = "Đủ tài nguyên để render nền.";
            return result;
        }
    }
}
