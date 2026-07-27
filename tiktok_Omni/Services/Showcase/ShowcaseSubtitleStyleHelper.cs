using System;

namespace tiktok_Omni.Services.Showcase
{
    public static class ShowcaseSubtitleStyleHelper
    {
        public static AssSubtitleGeneratorOptions BuildBodyOptions(ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            return BuildBodyOptionsWithLineOverride(video, fallbackSettings, null);
        }

        public static AssSubtitleGeneratorOptions BuildBodyOptionsWithLineOverride(
            ShowcaseVideoItem video,
            AppSettings fallbackSettings,
            string lineAnimationStorage)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var saved = video.ShowcaseSubtitleAnimation;
            if (!string.IsNullOrWhiteSpace(lineAnimationStorage))
            {
                video.ShowcaseSubtitleAnimation = lineAnimationStorage.Trim();
            }

            try
            {
                return BuildBodyOptionsCore(video, fallbackSettings);
            }
            finally
            {
                video.ShowcaseSubtitleAnimation = saved;
            }
        }

        private static AssSubtitleGeneratorOptions BuildBodyOptionsCore(ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var fontName = (video.ShowcaseSubtitleFontName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fontName))
            {
                fontName = "Segoe UI Bold";
            }

            var fontSize = Math.Max(28, Math.Min(160, video.ShowcaseSubtitleFontSize <= 0 ? 72 : video.ShowcaseSubtitleFontSize));
            var position = ReupSubtitleStyleHelper.ParsePosition(video.ShowcaseSubtitlePosition);
            var animation = ReupSubtitleStyleHelper.ParseAnimation(video.ShowcaseSubtitleAnimation);
            var settings = fallbackSettings ?? new AppSettings();

            return new AssSubtitleGeneratorOptions
            {
                FontName = fontName,
                FontSize = fontSize,
                Alignment = (int)position,
                MarginV = ReupSubtitleStyleHelper.ResolveMarginV(position, settings.ReupSubtitleMarginV),
                Animation = animation,
                WordsPerLine = 6,
                MinWordsPerLine = 2,
                RhythmicLineBreaks = true,
                Bold = video.ShowcaseSubtitleBold,
                Italic = video.ShowcaseSubtitleItalic,
                PopScalePercent = 150,
                PopDurationMs = 100
            };
        }

        public static AssSubtitleGeneratorOptions BuildHookOptions(ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            return BuildHookOptionsWithLineOverride(video, fallbackSettings, null);
        }

        public static AssSubtitleGeneratorOptions BuildHookOptionsWithLineOverride(
            ShowcaseVideoItem video,
            AppSettings fallbackSettings,
            string lineAnimationStorage)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var saved = video.ShowcaseHookSubtitleAnimation;
            if (!string.IsNullOrWhiteSpace(lineAnimationStorage))
            {
                video.ShowcaseHookSubtitleAnimation = lineAnimationStorage.Trim();
            }

            try
            {
                return BuildHookOptionsCore(video, fallbackSettings);
            }
            finally
            {
                video.ShowcaseHookSubtitleAnimation = saved;
            }
        }

        private static AssSubtitleGeneratorOptions BuildHookOptionsCore(ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var bodySize = video.ShowcaseSubtitleFontSize > 0 ? video.ShowcaseSubtitleFontSize : 72;
            var fontName = (video.ShowcaseHookSubtitleFontName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fontName))
            {
                fontName = (video.ShowcaseSubtitleFontName ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(fontName))
            {
                fontName = "Segoe UI Bold";
            }

            var hookSize = video.ShowcaseHookSubtitleFontSize > 0
                ? video.ShowcaseHookSubtitleFontSize
                : Math.Max(80, Math.Min(120, bodySize + 22));

            var animation = ShowcaseHookAnimationCatalog.ParseMode(video.ShowcaseHookSubtitleAnimation);
            var storage = (video.ShowcaseHookSubtitleAnimation ?? string.Empty).Trim();
            var popScale = ShowcaseHookAnimationCatalog.IsSlam(storage)
                ? 280
                : ShowcaseHookAnimationCatalog.IsNeonSale(storage)
                    ? 240
                    : animation == ReupKaraokeAnimationMode.Pop
                        ? 220
                        : animation == ReupKaraokeAnimationMode.Bounce
                            ? 200
                            : animation == ReupKaraokeAnimationMode.Shake
                                ? 210
                                : 150;

            var primaryColour = "&H00FFFF00";
            var secondaryColour = "&H0000FFFF";
            var outline = 10;
            if (ShowcaseHookAnimationCatalog.IsNeonSale(storage))
            {
                primaryColour = "&H004444FF";
                secondaryColour = "&H006666FF";
                outline = 14;
            }
            else if (animation == ReupKaraokeAnimationMode.GlowPulse)
            {
                primaryColour = "&H00FFFFFF";
                secondaryColour = "&H00FFFFCC";
                outline = 6;
            }

            return new AssSubtitleGeneratorOptions
            {
                FontName = fontName,
                FontSize = Math.Max(72, Math.Min(132, hookSize)),
                Alignment = 5,
                MarginV = 460,
                Animation = animation,
                WordsPerLine = 6,
                MinWordsPerLine = 2,
                RhythmicLineBreaks = true,
                Bold = true,
                Italic = false,
                PrimaryColourAss = primaryColour,
                SecondaryColourAss = secondaryColour,
                OutlineWidth = outline,
                PopScalePercent = popScale,
                PopDurationMs = ShowcaseHookAnimationCatalog.IsSlam(storage) ? 160 : 130
            };
        }

        public static AssSubtitleGeneratorOptions BuildOptions(ShowcaseVideoItem video, AppSettings fallbackSettings)
            => BuildBodyOptions(video, fallbackSettings);

        public static AssSubtitleGeneratorOptions BuildOptions(ShowcasePerVideoRenderSettings render, AppSettings fallbackSettings)
        {
            var video = new ShowcaseVideoItem
            {
                ShowcaseSubtitleFontName = render?.SubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = render?.SubtitleFontSize ?? 72,
                ShowcaseSubtitlePosition = render?.SubtitlePosition ?? "Bottom",
                ShowcaseSubtitleAnimation = render?.SubtitleAnimation ?? "Pop",
                ShowcaseSubtitleBold = render?.SubtitleBold ?? true,
                ShowcaseSubtitleItalic = render?.SubtitleItalic ?? false,
                ShowcaseSubtitleWordsPerLine = render?.SubtitleWordsPerLine ?? 6,
                ShowcaseSubtitleEnabled = render?.SubtitleEnabled ?? false,
                ShowcaseHookSubtitleEnabled = render?.HookSubtitleEnabled ?? false,
                ShowcaseHookSubtitleAnimation = render?.HookSubtitleAnimation ?? "PopStrong",
                ShowcaseHookSubtitleFontName = render?.HookSubtitleFontName ?? string.Empty,
                ShowcaseHookSubtitleFontSize = render?.HookSubtitleFontSize ?? 0
            };
            return BuildBodyOptions(video, fallbackSettings);
        }

        public static ShowcaseSubtitleRenderPlan BuildRenderPlan(ShowcasePerVideoRenderSettings render, AppSettings fallbackSettings)
        {
            var video = new ShowcaseVideoItem
            {
                ShowcaseSubtitleFontName = render?.SubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = render?.SubtitleFontSize ?? 72,
                ShowcaseSubtitlePosition = render?.SubtitlePosition ?? "Bottom",
                ShowcaseSubtitleAnimation = render?.SubtitleAnimation ?? "Pop",
                ShowcaseSubtitleBold = render?.SubtitleBold ?? true,
                ShowcaseSubtitleItalic = render?.SubtitleItalic ?? false,
                ShowcaseSubtitleWordsPerLine = render?.SubtitleWordsPerLine ?? 6,
                ShowcaseSubtitleEnabled = render?.SubtitleEnabled ?? false,
                ShowcaseHookSubtitleEnabled = render?.HookSubtitleEnabled ?? false,
                ShowcaseHookSubtitleAnimation = render?.HookSubtitleAnimation ?? "PopStrong",
                ShowcaseHookSubtitleFontName = render?.HookSubtitleFontName ?? string.Empty,
                ShowcaseHookSubtitleFontSize = render?.HookSubtitleFontSize ?? 0
            };
            var plan = BuildRenderPlan(video, fallbackSettings);
            ApplyOutputCanvas(plan, render, fallbackSettings);
            return plan;
        }

        public static void ApplyOutputCanvas(
            ShowcaseSubtitleRenderPlan plan,
            ShowcasePerVideoRenderSettings render,
            AppSettings fallbackSettings)
        {
            if (plan == null)
            {
                return;
            }

            var canvas = ShowcaseOutputAspectPresets.Resolve(
                render?.OutputAspectId,
                fallbackSettings?.ShowcaseOutputAspectDefault);
            ApplyOutputCanvas(plan, canvas);
        }

        public static void ApplyOutputCanvas(ShowcaseSubtitleRenderPlan plan, ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            if (plan == null || video == null)
            {
                return;
            }

            var canvas = ShowcaseOutputAspectPresets.Resolve(
                video.ShowcaseOutputAspectId,
                fallbackSettings?.ShowcaseOutputAspectDefault);
            ApplyOutputCanvas(plan, canvas);
        }

        private static void ApplyOutputCanvas(ShowcaseSubtitleRenderPlan plan, ShowcaseOutputAspectPreset canvas)
        {
            if (plan == null || canvas == null)
            {
                return;
            }

            if (plan.BodyOptions != null)
            {
                plan.BodyOptions.PlayResX = canvas.Width;
                plan.BodyOptions.PlayResY = canvas.Height;
            }

            if (plan.HookOptions != null)
            {
                plan.HookOptions.PlayResX = canvas.Width;
                plan.HookOptions.PlayResY = canvas.Height;
            }
        }

        public static ShowcaseSubtitleRenderPlan BuildRenderPlan(ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var plan = new ShowcaseSubtitleRenderPlan
            {
                HookEnabled = video.ShowcaseHookSubtitleEnabled,
                BodyEnabled = video.ShowcaseSubtitleEnabled,
                HookOptions = BuildHookOptions(video, fallbackSettings),
                BodyOptions = BuildBodyOptions(video, fallbackSettings)
            };
            ApplyOutputCanvas(plan, video, fallbackSettings);
            return plan;
        }

        public static ShowcaseVideoItem CreateStyleVideoFromRenderSettings(ShowcasePerVideoRenderSettings render)
        {
            if (render == null)
            {
                return new ShowcaseVideoItem();
            }

            return new ShowcaseVideoItem
            {
                ShowcaseSubtitleFontName = render.SubtitleFontName ?? string.Empty,
                ShowcaseSubtitleFontSize = render.SubtitleFontSize > 0 ? render.SubtitleFontSize : 72,
                ShowcaseSubtitlePosition = render.SubtitlePosition ?? "Bottom",
                ShowcaseSubtitleAnimation = render.SubtitleAnimation ?? "Pop",
                ShowcaseSubtitleBold = render.SubtitleBold,
                ShowcaseSubtitleItalic = render.SubtitleItalic,
                ShowcaseSubtitleWordsPerLine = render.SubtitleWordsPerLine > 0 ? render.SubtitleWordsPerLine : 6,
                ShowcaseSubtitleEnabled = render.SubtitleEnabled,
                ShowcaseHookSubtitleEnabled = render.HookSubtitleEnabled,
                ShowcaseHookSubtitleAnimation = render.HookSubtitleAnimation ?? "PopStrong",
                ShowcaseHookSubtitleFontName = render.HookSubtitleFontName ?? string.Empty,
                ShowcaseHookSubtitleFontSize = render.HookSubtitleFontSize
            };
        }

        public static void EnsureVideoDefaults(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return;
            }

            var s = settings ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(video.ShowcaseSubtitleFontName))
            {
                video.ShowcaseSubtitleFontName = string.IsNullOrWhiteSpace(s.ReupSubtitleFontName)
                    ? "Segoe UI Bold"
                    : s.ReupSubtitleFontName;
            }

            if (video.ShowcaseSubtitleFontSize <= 0)
            {
                video.ShowcaseSubtitleFontSize = s.ReupSubtitleFontSize <= 0 ? 72 : s.ReupSubtitleFontSize;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseSubtitlePosition))
            {
                video.ShowcaseSubtitlePosition = string.IsNullOrWhiteSpace(s.ReupSubtitlePosition)
                    ? "Bottom"
                    : s.ReupSubtitlePosition;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseSubtitleAnimation))
            {
                video.ShowcaseSubtitleAnimation = string.IsNullOrWhiteSpace(s.ReupSubtitleAnimation)
                    ? "Pop"
                    : s.ReupSubtitleAnimation;
            }

            if (string.IsNullOrWhiteSpace(video.ShowcaseHookSubtitleAnimation))
            {
                video.ShowcaseHookSubtitleAnimation = "PopStrong";
            }

            if (video.ShowcaseHookSubtitleFontSize <= 0 && video.ShowcaseSubtitleFontSize > 0)
            {
                video.ShowcaseHookSubtitleFontSize = Math.Max(80, Math.Min(120, video.ShowcaseSubtitleFontSize + 22));
            }

            if (video.ShowcaseSubtitleWordsPerLine <= 0)
            {
                video.ShowcaseSubtitleWordsPerLine = s.ReupSubtitleWordsPerLine <= 0 ? 6 : s.ReupSubtitleWordsPerLine;
            }

            video.ShowcaseTextSize = video.ShowcaseSubtitleFontSize;
            RefreshStyleLabel(video);
        }

        public static void ApplySettingsDefaultsToVideo(ShowcaseVideoItem video, AppSettings settings)
        {
            if (video == null)
            {
                return;
            }

            var s = settings ?? new AppSettings();
            video.ShowcaseSubtitleFontName = s.ReupSubtitleFontName ?? "Segoe UI Bold";
            video.ShowcaseSubtitleFontSize = s.ReupSubtitleFontSize <= 0 ? 72 : s.ReupSubtitleFontSize;
            video.ShowcaseSubtitlePosition = string.IsNullOrWhiteSpace(s.ReupSubtitlePosition) ? "Bottom" : s.ReupSubtitlePosition;
            video.ShowcaseSubtitleAnimation = string.IsNullOrWhiteSpace(s.ReupSubtitleAnimation) ? "Pop" : s.ReupSubtitleAnimation;
            video.ShowcaseSubtitleBold = s.ReupSubtitleBold;
            video.ShowcaseSubtitleItalic = s.ReupSubtitleItalic;
            video.ShowcaseSubtitleWordsPerLine = s.ReupSubtitleWordsPerLine <= 0 ? 6 : s.ReupSubtitleWordsPerLine;
            video.ShowcaseHookSubtitleAnimation = "PopStrong";
            video.ShowcaseHookSubtitleFontName = string.Empty;
            video.ShowcaseHookSubtitleFontSize = Math.Max(80, Math.Min(120, video.ShowcaseSubtitleFontSize + 22));
            video.ShowcaseTextSize = video.ShowcaseSubtitleFontSize;
            RefreshStyleLabel(video);
        }

        public static void RefreshStyleLabel(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return;
            }

            video.ShowcaseSubtitleStyleLabel = FormatStyleSummary(video);
        }

        public static string FormatStyleSummary(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return "Chưa cấu hình";
            }

            if (!video.ShowcaseHookSubtitleEnabled && !video.ShowcaseSubtitleEnabled)
            {
                return "⏸ Tắt · bấm mở bảng";
            }

            var layers = string.Empty;
            if (video.ShowcaseHookSubtitleEnabled && video.ShowcaseSubtitleEnabled)
            {
                layers = "Hook+Thân";
            }
            else if (video.ShowcaseHookSubtitleEnabled)
            {
                layers = "Hook";
            }
            else
            {
                layers = "Thân";
            }

            var hookFx = ShowcaseHookAnimationCatalog.DisplayLabel(video.ShowcaseHookSubtitleAnimation);
            var position = ReupSubtitleStyleHelper.PositionDisplayLabel(
                ReupSubtitleStyleHelper.ParsePosition(video.ShowcaseSubtitlePosition));
            var size = video.ShowcaseSubtitleFontSize > 0 ? video.ShowcaseSubtitleFontSize : 72;

            if (video.ShowcaseHookSubtitleEnabled && !video.ShowcaseSubtitleEnabled)
            {
                return "▶ " + layers + " · " + hookFx + " · giữa";
            }

            if (!video.ShowcaseHookSubtitleEnabled && video.ShowcaseSubtitleEnabled)
            {
                var anim = ReupSubtitleStyleHelper.AnimationDisplayLabel(
                    ReupSubtitleStyleHelper.ParseAnimation(video.ShowcaseSubtitleAnimation));
                return "▶ " + layers + " · " + position + " " + size + " · " + anim;
            }

            return "▶ " + layers + " · Hook " + hookFx + " · Thân " + position;
        }

        public static string HookAnimationDisplayLabel(string animation)
            => ShowcaseHookAnimationCatalog.DisplayLabel(animation);

        public static string HookAnimationToStorage(int selectedIndex)
            => ShowcaseHookAnimationCatalog.StorageFromSelectedIndex(selectedIndex);

        public static int HookAnimationToSelectedIndex(string animation)
            => ShowcaseHookAnimationCatalog.SelectedIndexFromStorage(animation);

        private static string ShortFontLabel(string fontName)
        {
            var font = (fontName ?? string.Empty).Trim();
            if (font.Length <= 14)
            {
                return string.IsNullOrEmpty(font) ? "Segoe UI" : font;
            }

            return font.Substring(0, 12) + "…";
        }
    }
}
