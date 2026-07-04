using System;
using System.Collections.Generic;
using System.IO;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Bọc <see cref="AssSubtitleGenerator"/> — nhận transcript plain text và ghi file .ass.</summary>
    public sealed class AssSubtitleGeneratorService
    {
        /// <summary>Thư mục ghi file ASS (mặc định: temp nếu không set).</summary>
        public string WorkDirectory { get; set; }

        /// <summary>Thời lượng fallback (ms) khi không có audio để đo.</summary>
        public double FallbackDurationMs { get; set; } = 6000d;

        /// <summary>Thời lượng audio (ms) — ưu tiên hơn đo file.</summary>
        public double? DurationMsOverride { get; set; }

        /// <summary>Tạo file .ass từ segment transcript (timing chính xác từ ElevenLabs / Whisper).</summary>
        public string GenerateFromSegments(IReadOnlyList<TranscriptSegment> segments, string styleName)
        {
            return GenerateFromSegments(segments, ResolveSubtitleStyle(styleName));
        }

        /// <summary>Tạo file .ass từ segment transcript.</summary>
        public string GenerateFromSegments(
            IReadOnlyList<TranscriptSegment> segments,
            AssSubtitleGeneratorOptions subtitleStyle)
        {
            if (segments == null || segments.Count == 0)
            {
                return string.Empty;
            }

            var opt = subtitleStyle ?? AffiliateVideoPostProcessingService.CreateKaraokeStyleOptions();
            var dir = (WorkDirectory ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Path.GetTempPath(), "tiktok_Omni_ass");
            }

            Directory.CreateDirectory(dir);
            var assPath = Path.Combine(dir, "hook_segments_" + Guid.NewGuid().ToString("N") + ".ass");
            AssSubtitleGenerator.WriteAssFileFromSegments(assPath, segments, opt);
            return assPath;
        }

        /// <summary>Tạo file .ass karaoke từ transcript ElevenLabs (tên style, ví dụ «Default»).</summary>
        public string Generate(string transcript, string styleName)
        {
            return Generate(transcript, ResolveSubtitleStyle(styleName));
        }

        /// <summary>Tạo file .ass karaoke từ transcript ElevenLabs.</summary>
        public string Generate(string transcript, AssSubtitleGeneratorOptions subtitleStyle)
        {
            var text = (transcript ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var opt = subtitleStyle ?? AffiliateVideoPostProcessingService.CreateKaraokeStyleOptions();
            var words = SubtitleTimingHelper.SplitWords(text);
            if (words.Count == 0)
            {
                return string.Empty;
            }

            var durationMs = ResolveDurationMs(text);
            var timestamps = SubtitleTimingHelper.EstimateWordTimestamps(text, durationMs);
            if (timestamps == null || timestamps.Count == 0)
            {
                return string.Empty;
            }

            var dir = (WorkDirectory ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Path.GetTempPath(), "tiktok_Omni_ass");
            }

            Directory.CreateDirectory(dir);
            var assPath = Path.Combine(dir, "hook_transcript_" + Guid.NewGuid().ToString("N") + ".ass");
            AssSubtitleGenerator.WriteAssFile(assPath, timestamps, opt);
            return assPath;
        }

        private double ResolveDurationMs(string text)
        {
            if (DurationMsOverride.HasValue && DurationMsOverride.Value >= 50d)
            {
                return DurationMsOverride.Value;
            }

            return Math.Max(1500d, FallbackDurationMs);
        }

        private static AssSubtitleGeneratorOptions ResolveSubtitleStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName) ||
                string.Equals(styleName, "Default", StringComparison.OrdinalIgnoreCase))
            {
                return AffiliateVideoPostProcessingService.CreateKaraokeStyleOptions();
            }

            return AffiliateVideoPostProcessingService.CreateKaraokeStyleOptions();
        }
    }
}
