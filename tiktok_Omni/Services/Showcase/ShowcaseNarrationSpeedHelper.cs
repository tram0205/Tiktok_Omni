using System;
using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Kế hoạch khớp thời lượng thoại ↔ video ghép (tua nhanh bên dài hơn).</summary>
    public sealed class ShowcaseAvSyncPlan
    {
        public double TargetDurationSeconds { get; set; }

        /// <summary>&gt;1 = tua nhanh audio (ngắn hơn).</summary>
        public double AudioTempo { get; set; } = 1d;

        /// <summary>&gt;1 = tua nhanh video (ngắn hơn).</summary>
        public double VideoTempo { get; set; } = 1d;
    }

    public sealed class ShowcaseAvSyncResult
    {
        public string VideoPath { get; set; } = string.Empty;

        public string NarrationPath { get; set; } = string.Empty;

        public double TargetDurationSeconds { get; set; }

        /// <summary>&gt;1 khi audio đã tua nhanh lúc khớp clip (phụ đề scale 1/tempo).</summary>
        public double AppliedAudioTempo { get; set; } = 1d;

        /// <summary>&gt;1 khi video đã tua nhanh lúc khớp thoại.</summary>
        public double AppliedVideoTempo { get; set; } = 1d;
    }

    public static class ShowcaseNarrationSpeedHelper
    {
        public const int AutoSpeedPercent = 0;

        public const int DefaultManualSpeedPercent = 100;

        public const int MinManualSpeedPercent = 50;

        public const int MaxManualSpeedPercent = 200;

        public static int ClampManualPercent(int percent)
        {
            if (percent <= 0)
            {
                return AutoSpeedPercent;
            }

            return Math.Max(MinManualSpeedPercent, Math.Min(MaxManualSpeedPercent, percent));
        }

        /// <summary>0 (cũ) → 100% — luôn có tốc độ nền trước khi tự khớp clip lúc render.</summary>
        public static int ResolveEffectiveSpeedPercent(int storedPercent)
        {
            if (storedPercent <= 0)
            {
                return DefaultManualSpeedPercent;
            }

            return ClampManualPercent(storedPercent);
        }

        /// <summary>Điền hook/thân từ giá trị cũ một cột nếu chưa lưu riêng.</summary>
        public static void EnsureSegmentSpeedDefaults(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            var legacy = ResolveEffectiveSpeedPercent(video.ShowcaseNarrationSpeedPercent);
            if (video.ShowcaseHookNarrationSpeedPercent <= 0)
            {
                video.ShowcaseHookNarrationSpeedPercent = legacy;
            }

            if (video.ShowcaseBodyNarrationSpeedPercent <= 0)
            {
                video.ShowcaseBodyNarrationSpeedPercent = legacy;
            }
        }

        /// <summary>Tốc độ render timeline (ưu tiên thân, rồi hook).</summary>
        public static int ResolveRenderSpeedPercent(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return DefaultManualSpeedPercent;
            }

            EnsureSegmentSpeedDefaults(video);
            if (video.ShowcaseBodyNarrationSpeedPercent > 0)
            {
                return ResolveEffectiveSpeedPercent(video.ShowcaseBodyNarrationSpeedPercent);
            }

            if (video.ShowcaseHookNarrationSpeedPercent > 0)
            {
                return ResolveEffectiveSpeedPercent(video.ShowcaseHookNarrationSpeedPercent);
            }

            return ResolveEffectiveSpeedPercent(video.ShowcaseNarrationSpeedPercent);
        }

        public static void SyncLegacyCombinedSpeedField(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            EnsureSegmentSpeedDefaults(video);
            video.ShowcaseNarrationSpeedPercent = video.ShowcaseBodyNarrationSpeedPercent;
        }

        public static string FormatSegmentSpeedGridLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return FormatGridLabel(DefaultManualSpeedPercent);
            }

            EnsureSegmentSpeedDefaults(video);
            var hook = ResolveEffectiveSpeedPercent(video.ShowcaseHookNarrationSpeedPercent);
            var body = ResolveEffectiveSpeedPercent(video.ShowcaseBodyNarrationSpeedPercent);
            if (hook == body)
            {
                return FormatGridLabel(hook);
            }

            return "hook " + hook.ToString(CultureInfo.InvariantCulture) + "% · thân "
                   + body.ToString(CultureInfo.InvariantCulture) + "% · tự khớp";
        }

        public static string FormatGridLabel(int speedPercent)
        {
            if (speedPercent <= 0)
            {
                return DefaultManualSpeedPercent.ToString(CultureInfo.InvariantCulture) + "% · tự khớp";
            }

            return speedPercent.ToString(CultureInfo.InvariantCulture) + "% · tự khớp";
        }

        public static ShowcaseAvSyncPlan ComputeAutoPlan(double narrationSeconds, double videoSeconds)
        {
            var narration = Math.Max(0.1d, narrationSeconds);
            var video = Math.Max(0.1d, videoSeconds);
            if (Math.Abs(narration - video) <= 0.08d)
            {
                return new ShowcaseAvSyncPlan
                {
                    TargetDurationSeconds = video,
                    AudioTempo = 1d,
                    VideoTempo = 1d
                };
            }

            if (narration > video)
            {
                return new ShowcaseAvSyncPlan
                {
                    TargetDurationSeconds = video,
                    AudioTempo = narration / video,
                    VideoTempo = 1d
                };
            }

            return new ShowcaseAvSyncPlan
            {
                TargetDurationSeconds = narration,
                AudioTempo = 1d,
                VideoTempo = video / narration
            };
        }

        public static ShowcaseAvSyncPlan BuildRenderPlan(
            double narrationSeconds,
            double videoSeconds,
            int userSpeedPercent)
        {
            var baseTempo = ResolveEffectiveSpeedPercent(userSpeedPercent) / 100d;
            var adjustedNarrationDuration = Math.Max(0.1d, narrationSeconds) / baseTempo;
            var autoPlan = ComputeAutoPlan(adjustedNarrationDuration, Math.Max(0.1d, videoSeconds));
            return new ShowcaseAvSyncPlan
            {
                TargetDurationSeconds = autoPlan.TargetDurationSeconds,
                AudioTempo = baseTempo * autoPlan.AudioTempo,
                VideoTempo = autoPlan.VideoTempo
            };
        }
    }
}
