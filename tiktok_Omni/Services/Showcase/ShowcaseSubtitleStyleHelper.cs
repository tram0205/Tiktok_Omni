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
            string lineAnimationStorage,
            ShowcaseSubtitleLineRenderOverride lineStyle = null)
        {
            EnsureVideoDefaults(video, fallbackSettings);
            var saved = video.ShowcaseSubtitleAnimation;
            if (!string.IsNullOrWhiteSpace(lineAnimationStorage))
            {
                video.ShowcaseSubtitleAnimation = lineAnimationStorage.Trim();
            }

            try
            {
                var opts = BuildBodyOptionsCore(video, fallbackSettings);
                ApplyLineStyleOverride(opts, lineStyle);
                return opts;
            }
            finally
            {
                video.ShowcaseSubtitleAnimation = saved;
            }
        }

        private static void ApplyLineStyleOverride(
            AssSubtitleGeneratorOptions opts,
            ShowcaseSubtitleLineRenderOverride lineStyle)
        {
            if (opts == null || lineStyle == null || !lineStyle.HasAny())
            {
                return;
            }

            var look = (lineStyle.LookPreset ?? string.Empty).Trim();
            if (look.Length > 0)
            {
                ShowcaseSubtitleLookPresetCatalog.ApplyToOptions(opts, look, ShowcaseDisplayLineEffectKind.Body);
            }

            var fontName = (lineStyle.FontName ?? string.Empty).Trim();
            if (fontName.Length > 0)
            {
                ShowcaseSubtitleFontHelper.ApplyToOptions(opts, fontName, opts.Bold, opts.Italic);
            }

            if (lineStyle.FontSize > 0)
            {
                opts.FontSize = Math.Max(28, Math.Min(160, lineStyle.FontSize));
            }

            if (look.Length == 0)
            {
                var face = (lineStyle.FontFace ?? string.Empty).Trim();
                if (string.Equals(face, "Bold", StringComparison.OrdinalIgnoreCase))
                {
                    opts.Bold = true;
                    opts.Italic = false;
                }
                else if (string.Equals(face, "Italic", StringComparison.OrdinalIgnoreCase))
                {
                    opts.Bold = false;
                    opts.Italic = true;
                }
                else if (string.Equals(face, "Regular", StringComparison.OrdinalIgnoreCase))
                {
                    opts.Bold = false;
                    opts.Italic = false;
                }

                var colour = (lineStyle.PrimaryColourAss ?? string.Empty).Trim();
                if (colour.Length > 0)
                {
                    opts.PrimaryColourAss = colour;
                    opts.SecondaryColourAss = ShowcaseSubtitleColourPresetCatalog.SecondaryAssFromPrimaryAss(colour);
                }

                var decor = (lineStyle.DecorPreset ?? string.Empty).Trim();
                if (decor.Length > 0)
                {
                    ShowcaseSubtitleDecorPresetCatalog.Apply(opts, decor);
                }
            }

            var pos = (lineStyle.Position ?? string.Empty).Trim();
            if (pos.Length > 0)
            {
                opts.Alignment = (int)ReupSubtitleStyleHelper.ParsePosition(pos);
            }

            var lineBg = (lineStyle.LineBackgroundColourAss ?? string.Empty).Trim();
            if (lineBg.Length > 0)
            {
                opts.LineBackgroundColourAss = lineBg;
            }

            ShowcaseSubtitleFontHelper.ApplyToOptions(opts, opts.FontName, opts.Bold, opts.Italic);
        }

        private static void ApplyAnimationStorageToOptions(AssSubtitleGeneratorOptions opts, string storage, bool forHook)
        {
            if (opts == null)
            {
                return;
            }

            var value = (storage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || string.Equals(value, "Pop", StringComparison.OrdinalIgnoreCase))
            {
                value = forHook ? ShowcaseHookAnimationCatalog.DefaultStorage : "Pop";
            }

            if (!forHook && string.Equals(value, "Plain", StringComparison.OrdinalIgnoreCase))
            {
                opts.Animation = ReupKaraokeAnimationMode.Plain;
                opts.PopScalePercent = 150;
                opts.PopDurationMs = 100;
                return;
            }

            var animation = ShowcaseHookAnimationCatalog.ParseMode(value);
            opts.Animation = animation;

            if (ShowcaseHookAnimationCatalog.IsSlam(value))
            {
                opts.PopScalePercent = forHook ? 280 : 220;
                opts.PopDurationMs = forHook ? 160 : 140;
            }
            else if (ShowcaseHookAnimationCatalog.IsNeonSale(value))
            {
                opts.PopScalePercent = forHook ? 240 : 200;
                opts.PopDurationMs = forHook ? 130 : 120;
                opts.PrimaryColourAss = "&H004444FF";
                opts.SecondaryColourAss = "&H006666FF";
                opts.OutlineWidth = Math.Max(opts.OutlineWidth, 14);
            }
            else if (animation == ReupKaraokeAnimationMode.Pop)
            {
                opts.PopScalePercent = forHook ? 220 : 165;
                opts.PopDurationMs = forHook ? 130 : 105;
            }
            else if (animation == ReupKaraokeAnimationMode.Bounce)
            {
                opts.PopScalePercent = forHook ? 200 : 175;
                opts.PopDurationMs = forHook ? 130 : 115;
            }
            else if (animation == ReupKaraokeAnimationMode.Shake)
            {
                opts.PopScalePercent = forHook ? 210 : 180;
                opts.PopDurationMs = forHook ? 130 : 110;
            }
            else if (animation == ReupKaraokeAnimationMode.GlowPulse)
            {
                opts.PopScalePercent = forHook ? 150 : 150;
                opts.PopDurationMs = forHook ? 130 : 110;
                if (string.IsNullOrWhiteSpace(opts.PrimaryColourAss))
                {
                    opts.PrimaryColourAss = "&H00FFFFFF";
                    opts.SecondaryColourAss = "&H00FFFFCC";
                }

                if (opts.OutlineWidth <= 10)
                {
                    opts.OutlineWidth = 6;
                }
            }
            else
            {
                opts.PopScalePercent = forHook ? 150 : 150;
                opts.PopDurationMs = forHook ? 130 : 100;
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
            var settings = fallbackSettings ?? new AppSettings();
            var storage = (video.ShowcaseSubtitleAnimation ?? string.Empty).Trim();

            var opts = new AssSubtitleGeneratorOptions
            {
                FontName = fontName,
                FontSize = fontSize,
                Alignment = (int)position,
                MarginV = ReupSubtitleStyleHelper.ResolveMarginV(position, settings.ReupSubtitleMarginV),
                WordsPerLine = Math.Max(4, Math.Min(12, video.ShowcaseSubtitleWordsPerLine > 0 ? video.ShowcaseSubtitleWordsPerLine : 6)),
                MinWordsPerLine = 2,
                RhythmicLineBreaks = true,
                Bold = video.ShowcaseSubtitleBold,
                Italic = video.ShowcaseSubtitleItalic
            };

            ApplyAnimationStorageToOptions(opts, storage, forHook: false);

            var bodyLook = (video.ShowcaseSubtitleLookPreset ?? string.Empty).Trim();
            if (bodyLook.Length > 0)
            {
                ShowcaseSubtitleLookPresetCatalog.ApplyToOptions(opts, bodyLook, ShowcaseDisplayLineEffectKind.Body);
            }
            else
            {
                var bodyColour = (video.ShowcaseSubtitlePrimaryColourAss ?? string.Empty).Trim();
                if (bodyColour.Length > 0)
                {
                    opts.PrimaryColourAss = bodyColour;
                    opts.SecondaryColourAss = ShowcaseSubtitleColourPresetCatalog.SecondaryAssFromPrimaryAss(bodyColour);
                }

                ShowcaseSubtitleDecorPresetCatalog.Apply(opts, video.ShowcaseSubtitleDecorPreset);
            }

            ApplyHighlightSecondaryOverride(opts, video.ShowcaseSubtitleHighlightColourAss);
            ShowcaseSubtitleFontHelper.ApplyToOptions(opts, opts.FontName, opts.Bold, opts.Italic);
            return opts;
        }

        private static void ApplyHighlightSecondaryOverride(AssSubtitleGeneratorOptions opts, string highlightAss)
        {
            var ass = (highlightAss ?? string.Empty).Trim();
            if (ass.Length > 0 && opts != null)
            {
                opts.LineBackgroundColourAss = ass;
            }
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

            var storage = (video.ShowcaseHookSubtitleAnimation ?? string.Empty).Trim();

            var opts = new AssSubtitleGeneratorOptions
            {
                FontName = fontName,
                FontSize = Math.Max(72, Math.Min(132, hookSize)),
                Alignment = 5,
                MarginV = 460,
                WordsPerLine = 6,
                MinWordsPerLine = 2,
                RhythmicLineBreaks = true,
                Bold = true,
                Italic = false,
                PrimaryColourAss = "&H0000FFFF",
                SecondaryColourAss = "&H00FFFF00",
                OutlineWidth = 10
            };

            ApplyAnimationStorageToOptions(opts, storage, forHook: true);

            var hookLook = (video.ShowcaseHookSubtitleLookPreset ?? string.Empty).Trim();
            if (hookLook.Length > 0)
            {
                ShowcaseSubtitleLookPresetCatalog.ApplyToOptions(opts, hookLook, ShowcaseDisplayLineEffectKind.Hook);
            }
            else
            {
                var hookColour = (video.ShowcaseHookSubtitlePrimaryColourAss ?? string.Empty).Trim();
                if (hookColour.Length > 0)
                {
                    opts.PrimaryColourAss = hookColour;
                    opts.SecondaryColourAss = ShowcaseSubtitleColourPresetCatalog.SecondaryAssFromPrimaryAss(hookColour);
                }

                ShowcaseSubtitleDecorPresetCatalog.Apply(opts, video.ShowcaseHookSubtitleDecorPreset);
            }

            ApplyHighlightSecondaryOverride(opts, video.ShowcaseHookSubtitleHighlightColourAss);

            ShowcaseSubtitleFontHelper.ApplyToOptions(opts, opts.FontName, opts.Bold, opts.Italic);
            return opts;
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

            var canvas = render?.ResolveOutputCanvas(fallbackSettings) ?? ShowcaseOutputAspectPresets.Vertical9x16;
            ApplyOutputCanvas(plan, canvas);
        }

        public static void ApplyOutputCanvas(ShowcaseSubtitleRenderPlan plan, ShowcaseVideoItem video, AppSettings fallbackSettings)
        {
            if (plan == null || video == null)
            {
                return;
            }

            var canvas = ShowcaseOutputAspectPresets.ResolveForVideo(video, fallbackSettings?.ShowcaseOutputAspectDefault);
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
