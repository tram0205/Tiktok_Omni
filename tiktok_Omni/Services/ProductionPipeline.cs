using System;
using System.IO;

namespace tiktok_Omni.Services
{
    /// <summary>Trạng thái thống nhất cho 4 tab sản xuất (Slideshow / Deep / Mascot / Philosophy).</summary>
    public static class ProductionPipeline
    {
        public const int ManualApprovalSafetyThreshold = 85;

        public static string MapRenderStageToStatus(string stage, int percent)
        {
            var s = (stage ?? string.Empty).Trim();
            if (percent >= 100 || s.IndexOf("Completed", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Xong", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Xong";
            }

            if (s.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("tải", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Starting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang tải";
            }

            if (s.IndexOf("narration", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Gemini", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("kịch bản", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Tạo ảnh", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("motion", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("sinh AI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang sinh AI";
            }

            if (s.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("merge", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("transition", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Final", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Veo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang render";
            }

            return percent > 0 ? "Đang render" : "Chờ";
        }

        public static string MapMascotStageToStatus(string stage, int percent)
        {
            return MapRenderStageToStatus(stage, percent);
        }

        public static string MapPhilosophyStatus(string statusText, int percent)
        {
            var t = (statusText ?? string.Empty).Trim();
            if (percent >= 100)
            {
                return "Xong";
            }

            if (t.IndexOf("tải", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("download", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang tải";
            }

            if (t.IndexOf("Gemini", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("quote", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("TTS", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Lyria", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("Veo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang sinh AI";
            }

            if (t.IndexOf("render", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("FFmpeg", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("ghép", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Đang render";
            }

            return "Đang render";
        }

        public static bool RequiresManualApproval(int safetyScore) =>
            safetyScore < ManualApprovalSafetyThreshold;

        public static string ResolveThumbnailPath(string videoPath)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return string.Empty;
            }

            var dir = Path.GetDirectoryName(videoPath) ?? string.Empty;
            var videoName = Path.GetFileNameWithoutExtension(videoPath);
            if (!string.IsNullOrWhiteSpace(videoName))
            {
                var namedCover = Path.Combine(dir, videoName + "_cover.jpg");
                if (File.Exists(namedCover))
                {
                    return namedCover;
                }
            }

            var cover = Path.Combine(dir, "_cover.jpg");
            if (File.Exists(cover))
            {
                return cover;
            }

            return videoPath;
        }
    }
}
