using System;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Cấu hình render một video Triết lý (thư mục nền, nhạc, giọng).</summary>
    public sealed class PhilosophyRenderOptions
    {
        public string BRollFolder { get; set; } = string.Empty;

        public string MusicFolder { get; set; } = string.Empty;

        /// <summary>Âm lượng nhạc nền (0–100%) khi mix audio.</summary>
        public int MusicVolumePercent { get; set; } = PhilosophyBatchHelper.DefaultMusicVolumePercent;

        /// <summary>Tốc độ thoại (50–200%) khi mix audio.</summary>
        public int NarrationSpeedPercent { get; set; } = ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;

        /// <summary>TTS/giọng từ popup Âm thanh batch — null = fallback mood + ElevenLabs.</summary>
        public ShowcaseTtsRenderOptions TtsOptions { get; set; }

        /// <summary>Thư mục SFX môi trường (mưa/gió) — tùy chọn.</summary>
        public string AmbientFolder { get; set; } = string.Empty;

        public string VoiceId { get; set; } = string.Empty;

        public string ProfileName { get; set; } = "default";

        public AssSubtitleGeneratorOptions SubtitleOptions { get; set; }

        /// <summary>0 = B-Roll có sẵn, 1 = Veo T2V cảnh vật, 2 = Veo I2V mascot, 3 = Video tự làm sẵn.</summary>
        public int VisualMode { get; set; } = 0;

        /// <summary>Mode 3: thư mục chứa video phân cảnh tự làm sẵn.</summary>
        public string PreRenderedFolder { get; set; } = string.Empty;

        /// <summary>Mode 3: nội dung câu triết lý dùng để build prefix tên file quy ước.</summary>
        public string QuoteForSceneMatch { get; set; } = string.Empty;

        /// <summary>Ảnh tham chiếu từ popup Nội dung (batch) — ưu tiên cho Veo I2V mode 2.</summary>
        public string ReferenceImagePath { get; set; } = string.Empty;

        /// <summary>Đường dẫn ffmpeg.exe (tùy chọn — nếu trống thì tự resolve từ toolkit).</summary>
        public string FfmpegExe { get; set; } = string.Empty;

        public string StorageRootPath { get; set; } = string.Empty;

        /// <summary>Thời lượng video tối thiểu (giây) — mục tiêu Gemini/TTS khi tạo kịch bản.</summary>
        public int MinDurationSeconds { get; set; } = 15;

        /// <summary>Thời lượng video tối đa (giây) — mục tiêu Gemini/TTS khi tạo kịch bản.</summary>
        public int MaxDurationSeconds { get; set; } = 60;

        public bool BrandLogoEnabled { get; set; } = true;

        public string BrandLogoFile { get; set; } = string.Empty;

        public string BrandLogoPositionId { get; set; } = string.Empty;

        public int BrandLogoScaleWidthPercent { get; set; }

        public int BrandLogoMarginX { get; set; }

        public int BrandLogoMarginY { get; set; }

        public int BrandLogoOpacityPercent { get; set; }

        /// <summary>Khoảng lặng tối thiểu sau khi đọc hết quote.</summary>
        public const double OutroPadMinSeconds = 3d;

        /// <summary>Khoảng lặng tối đa sau khi đọc hết quote.</summary>
        public const double OutroPadMaxSeconds = 6d;

        /// <summary>Outro linh hoạt 3–6s: dùng phần video nền còn lại sau giọng đọc (phân cảnh cuối).</summary>
        public static double ResolveOutroPadSeconds(double voiceSeconds, double backgroundSeconds)
        {
            var voice = Math.Max(0.5d, voiceSeconds);
            if (backgroundSeconds < 0.1d)
            {
                return OutroPadMaxSeconds;
            }

            var remaining = backgroundSeconds - voice;
            if (remaining <= OutroPadMinSeconds)
            {
                return OutroPadMinSeconds;
            }

            if (remaining >= OutroPadMaxSeconds)
            {
                return OutroPadMaxSeconds;
            }

            return remaining;
        }

        /// <summary>Thời lượng xuất = đọc hết giọng TTS + outro 3–6s (tùy video nền).</summary>
        public static double ResolveOutputDuration(double voiceSeconds, double backgroundSeconds = -1d)
        {
            var voice = Math.Max(0.5d, voiceSeconds);
            var pad = backgroundSeconds >= 0d
                ? ResolveOutroPadSeconds(voice, backgroundSeconds)
                : OutroPadMaxSeconds;
            return voice + pad;
        }

        public static (int MinSeconds, int MaxSeconds) NormalizeDurationBounds(int minSeconds, int maxSeconds)
        {
            var min = Math.Max(5, Math.Min(180, minSeconds));
            var max = Math.Max(5, Math.Min(180, maxSeconds));
            if (min > max)
            {
                var swap = min;
                min = max;
                max = swap;
            }

            return (min, max);
        }

        /// <summary>Ước lượng số từ cho TTS triết lý (ElevenLabs v3 + ngắt nghỉ sâu, tiếng Việt).</summary>
        public static (int MinWords, int MaxWords) EstimateSpeechWordCount(int minSeconds, int maxSeconds)
        {
            var (min, max) = NormalizeDurationBounds(minSeconds, maxSeconds);
            // ~1.6 từ/giây nói thực tế — cao hơn WPM sách vì prompt Gemini cần khớp thời lượng đọc thực.
            const double wordsPerSecond = 1.6d;
            var minWords = (int)Math.Max(10, Math.Round(min * wordsPerSecond * 0.94d));
            var maxWords = (int)Math.Max(minWords + 4, Math.Round(max * wordsPerSecond * 1.02d));
            return (minWords, maxWords);
        }
    }
}
