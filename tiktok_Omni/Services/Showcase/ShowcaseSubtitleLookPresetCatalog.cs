using System;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Preset gom font + đậm/nghiêng + màu + viền/bóng — một lựa chọn cho phụ đề tiếng Việt.</summary>
    public static class ShowcaseSubtitleLookPresetCatalog
    {
        public const string BodyDefaultStorage = "TikTokWhite";
        public const string HookDefaultStorage = "HookGoldBold";

        public sealed class Preset
        {
            public Preset(
                string storage,
                string label,
                string fontName,
                bool bold,
                bool italic,
                string primaryAss,
                string secondaryAss,
                int outline,
                int shadow,
                string decorStorage)
            {
                Storage = storage ?? string.Empty;
                Label = label ?? string.Empty;
                FontName = fontName ?? "Segoe UI";
                Bold = bold;
                Italic = italic;
                PrimaryAss = primaryAss ?? "&H00FFFFFF";
                SecondaryAss = secondaryAss ?? "&H00D7FF00";
                OutlineWidth = outline;
                ShadowDepth = shadow;
                DecorStorage = decorStorage ?? string.Empty;
            }

            public string Storage { get; }

            public string Label { get; }

            public string FontName { get; }

            public bool Bold { get; }

            public bool Italic { get; }

            public string PrimaryAss { get; }

            public string SecondaryAss { get; }

            public int OutlineWidth { get; }

            public int ShadowDepth { get; }

            public string DecorStorage { get; }
        }

        public static readonly Preset[] BodyAll =
        {
            new Preset("TikTokWhite", "TikTok · Trắng viền đen", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 10, 2, string.Empty),
            new Preset("TikTokClean", "Sạch · Trắng viền mỏng", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 6, 1, string.Empty),
            new Preset("SoftWhite", "Mềm · Trắng nhẹ", "Segoe UI", false, false, "&H00FFFFFF", "&H00D7FF00", 8, 2, string.Empty),
            new Preset("ReviewBold", "Review · Đậm rõ", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 10, 3, string.Empty),
            new Preset("VerdanaClassic", "Classic · Đậm", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 9, 2, string.Empty),
            new Preset("StoryItalic", "Kể chuyện · Trắng nghiêng", "Segoe UI", false, true, "&H00FFFFFF", "&H00D7FF00", 8, 2, string.Empty),
            new Preset("YellowAccent", "Nhấn · Vàng TikTok", "Segoe UI", true, false, "&H0000FFFF", "&H00FFFF00", 12, 3, "ThickOutline"),
            new Preset("CyanNeon", "Neon · Cyan", "Segoe UI", true, false, "&H00FFFF00", "&H00FFFFFF", 14, 1, "SoftGlow"),
            new Preset("PinkTrend", "Trend · Hồng neon", "Segoe UI", true, false, "&H00C080FF", "&H00FFFFFF", 14, 1, "SoftGlow"),
            new Preset("GreenFresh", "Tươi · Xanh lá", "Segoe UI", true, false, "&H0000FF00", "&H00FFFFFF", 10, 2, string.Empty),
            new Preset("CinemaWhite", "Điện ảnh · Trắng bóng", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 16, 6, "Cinema"),
            new Preset("DeepShadow", "Nổi · Bóng đậm", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 8, 14, "DeepShadow"),
            new Preset("GlowEdge", "Glow · Viền sáng", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 14, 1, "SoftGlow"),
            new Preset("OutlineHeavy", "Viền · Dày rõ", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 18, 3, "ThickOutline"),
            new Preset("DarkOnLight", "Nền sáng · Đen viền trắng", "Segoe UI", true, false, "&H00000000", "&H00FFFFFF", 14, 2, string.Empty),
            new Preset("CreamWarm", "Ấm · Kem viền nâu", "Segoe UI", true, false, "&H00DCF5FF", "&H00FFFFFF", 8, 2, string.Empty),
            new Preset("AffiliateOrange", "CTA · Cam sale", "Segoe UI", true, false, "&H000080FF", "&H00FFFFFF", 12, 3, "ThickOutline"),
            new Preset("PlainRegular", "Tối giản · Thường", "Segoe UI", false, false, "&H00FFFFFF", "&H00D7FF00", 6, 1, string.Empty)
        };

        public static readonly Preset[] HookAll =
        {
            new Preset("HookGoldBold", "Hook · Vàng đậm", "Segoe UI", true, false, "&H0000FFFF", "&H00FFFF00", 12, 3, "ThickOutline"),
            new Preset("HookGoldImpact", "Hook · Vàng nặng", "Segoe UI", true, false, "&H0000FFFF", "&H00FFFF00", 14, 2, string.Empty),
            new Preset("HookWhiteSlam", "Hook · Trắng slam", "Segoe UI", true, false, "&H00FFFFFF", "&H00D7FF00", 14, 3, string.Empty),
            new Preset("HookNeonCyan", "Hook · Neon cyan", "Segoe UI", true, false, "&H00FFFF00", "&H00FFFFFF", 16, 1, "SoftGlow"),
            new Preset("HookNeonPink", "Hook · Neon hồng", "Segoe UI", true, false, "&H00C080FF", "&H00FFFFFF", 16, 1, "SoftGlow"),
            new Preset("HookRedFlash", "Hook · Đỏ flash", "Segoe UI", true, false, "&H000000FF", "&H00FFFFFF", 12, 3, "ThickOutline"),
            new Preset("HookOrangeHot", "Hook · Cam nóng", "Segoe UI", true, false, "&H000080FF", "&H00FFFFFF", 14, 2, string.Empty),
            new Preset("HookCinema", "Hook · Điện ảnh vàng", "Segoe UI", true, false, "&H0000FFFF", "&H00FFFF00", 16, 6, "Cinema"),
            new Preset("HookGlowGold", "Hook · Vàng glow", "Segoe UI", true, false, "&H0000FFFF", "&H00FFFF00", 14, 1, "SoftGlow"),
            new Preset("HookBlackPop", "Hook · Đen viền trắng", "Segoe UI", true, false, "&H00000000", "&H00FFFFFF", 16, 2, string.Empty)
        };

        public static Preset[] AllForKind(ShowcaseDisplayLineEffectKind kind)
        {
            return kind == ShowcaseDisplayLineEffectKind.Hook ? HookAll : BodyAll;
        }

        public static Preset FindByStorage(string storage, ShowcaseDisplayLineEffectKind kind)
        {
            var key = (storage ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return null;
            }

            return AllForKind(kind).FirstOrDefault(p =>
                string.Equals(p.Storage, key, StringComparison.OrdinalIgnoreCase));
        }

        public static Preset FindByLabel(string label, ShowcaseDisplayLineEffectKind kind)
        {
            var text = (label ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            var preset = AllForKind(kind).FirstOrDefault(p =>
                string.Equals(p.Label, text, StringComparison.Ordinal));
            if (preset != null)
            {
                return preset;
            }

            var legacyStorage = LegacyLabelToStorage(text, kind);
            if (legacyStorage.Length > 0)
            {
                return FindByStorage(legacyStorage, kind);
            }

            return null;
        }

        private static string LegacyLabelToStorage(string label, ShowcaseDisplayLineEffectKind kind)
        {
            if (kind == ShowcaseDisplayLineEffectKind.Hook)
            {
                if (string.Equals(label, "Hook · Vàng Segoe đậm", StringComparison.Ordinal))
                {
                    return "HookGoldBold";
                }

                if (string.Equals(label, "Hook · Vàng Impact", StringComparison.Ordinal))
                {
                    return "HookGoldImpact";
                }

                return string.Empty;
            }

            if (string.Equals(label, "Review · Arial đậm", StringComparison.Ordinal))
            {
                return "ReviewBold";
            }

            if (string.Equals(label, "Classic · Verdana đậm", StringComparison.Ordinal))
            {
                return "VerdanaClassic";
            }

            return string.Empty;
        }

        public static string StorageFromLabel(string label, ShowcaseDisplayLineEffectKind kind)
        {
            var preset = FindByLabel(label, kind);
            if (preset != null)
            {
                return preset.Storage;
            }

            return kind == ShowcaseDisplayLineEffectKind.Hook ? HookDefaultStorage : BodyDefaultStorage;
        }

        public static string LabelFromStorage(string storage, ShowcaseDisplayLineEffectKind kind)
        {
            var preset = FindByStorage(storage, kind);
            if (preset != null)
            {
                return preset.Label;
            }

            var fallback = kind == ShowcaseDisplayLineEffectKind.Hook ? HookAll[0] : BodyAll[0];
            return fallback.Label;
        }

        public static void PopulateCombo(ComboBox combo, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null)
            {
                return;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var p in AllForKind(kind))
            {
                combo.Items.Add(p.Label);
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        public static void PopulateComboColumn(DataGridViewComboBoxColumn column, ShowcaseDisplayLineEffectKind kind)
        {
            if (column == null)
            {
                return;
            }

            column.Items.Clear();
            foreach (var p in AllForKind(kind))
            {
                column.Items.Add(p.Label);
            }
        }

        public static void SelectLabel(ComboBox combo, string storage, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null)
            {
                return;
            }

            var label = LabelFromStorage(storage, kind);
            if (combo.Items.Contains(label))
            {
                combo.SelectedItem = label;
            }
            else if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        public static void SelectLabelCell(DataGridViewComboBoxCell cell, string storage, ShowcaseDisplayLineEffectKind kind)
        {
            if (cell == null)
            {
                return;
            }

            cell.Value = LabelFromStorage(storage, kind);
        }

        public static void ApplyToOptions(AssSubtitleGeneratorOptions opts, string storage, ShowcaseDisplayLineEffectKind kind)
        {
            if (opts == null)
            {
                return;
            }

            var preset = FindByStorage(storage, kind) ?? FindByStorage(
                kind == ShowcaseDisplayLineEffectKind.Hook ? HookDefaultStorage : BodyDefaultStorage,
                kind);
            if (preset == null)
            {
                return;
            }

            ShowcaseSubtitleFontHelper.ApplyToOptions(opts, preset.FontName, preset.Bold, preset.Italic);
            opts.PrimaryColourAss = preset.PrimaryAss;
            opts.SecondaryColourAss = preset.SecondaryAss;
            opts.OutlineWidth = preset.OutlineWidth;
            opts.ShadowDepth = preset.ShadowDepth;
        }

        public static void SyncBodyVideoFields(ShowcaseVideoItem video, string storage)
        {
            if (video == null)
            {
                return;
            }

            var preset = FindByStorage(storage, ShowcaseDisplayLineEffectKind.Body)
                         ?? FindByStorage(BodyDefaultStorage, ShowcaseDisplayLineEffectKind.Body);
            if (preset == null)
            {
                return;
            }

            video.ShowcaseSubtitleLookPreset = preset.Storage;
            video.ShowcaseSubtitleFontName = preset.FontName;
            video.ShowcaseSubtitleBold = preset.Bold;
            video.ShowcaseSubtitleItalic = preset.Italic;
            video.ShowcaseSubtitlePrimaryColourAss = preset.PrimaryAss;
            video.ShowcaseSubtitleDecorPreset = preset.DecorStorage;
        }

        public static void SyncHookVideoFields(ShowcaseVideoItem video, string storage)
        {
            if (video == null)
            {
                return;
            }

            var preset = FindByStorage(storage, ShowcaseDisplayLineEffectKind.Hook)
                         ?? FindByStorage(HookDefaultStorage, ShowcaseDisplayLineEffectKind.Hook);
            if (preset == null)
            {
                return;
            }

            video.ShowcaseHookSubtitleLookPreset = preset.Storage;
            video.ShowcaseHookSubtitleFontName = preset.FontName;
            video.ShowcaseHookSubtitlePrimaryColourAss = preset.PrimaryAss;
            video.ShowcaseHookSubtitleDecorPreset = preset.DecorStorage;
        }

        public static void SyncSceneDisplayFields(AiVideoGenInputItem scene, string storage)
        {
            if (scene == null)
            {
                return;
            }

            var preset = FindByStorage(storage, ShowcaseDisplayLineEffectKind.Body);
            if (preset == null)
            {
                scene.ShowcaseSubtitleDisplayLookPreset = (storage ?? string.Empty).Trim();
                return;
            }

            scene.ShowcaseSubtitleDisplayLookPreset = preset.Storage;
            scene.ShowcaseSubtitleDisplayFontName = preset.FontName;
            scene.ShowcaseSubtitleDisplayFontFace = preset.Bold
                ? "Bold"
                : preset.Italic ? "Italic" : "Regular";
            scene.ShowcaseSubtitleDisplayPrimaryColourAss = preset.PrimaryAss;
            scene.ShowcaseSubtitleDisplayDecorPreset = preset.DecorStorage;
        }

        public static string ResolveBodyStorageFromLegacy(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return BodyDefaultStorage;
            }

            var stored = (video.ShowcaseSubtitleLookPreset ?? string.Empty).Trim();
            if (FindByStorage(stored, ShowcaseDisplayLineEffectKind.Body) != null)
            {
                return stored;
            }

            return MatchLegacyToStorage(
                video.ShowcaseSubtitleFontName,
                video.ShowcaseSubtitleBold,
                video.ShowcaseSubtitleItalic,
                video.ShowcaseSubtitlePrimaryColourAss,
                video.ShowcaseSubtitleDecorPreset,
                ShowcaseDisplayLineEffectKind.Body);
        }

        public static string ResolveHookStorageFromLegacy(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return HookDefaultStorage;
            }

            var stored = (video.ShowcaseHookSubtitleLookPreset ?? string.Empty).Trim();
            if (FindByStorage(stored, ShowcaseDisplayLineEffectKind.Hook) != null)
            {
                return stored;
            }

            return MatchLegacyToStorage(
                video.ShowcaseHookSubtitleFontName,
                true,
                false,
                video.ShowcaseHookSubtitlePrimaryColourAss,
                video.ShowcaseHookSubtitleDecorPreset,
                ShowcaseDisplayLineEffectKind.Hook);
        }

        public static string ResolveSceneStorageFromLegacy(AiVideoGenInputItem scene)
        {
            if (scene == null)
            {
                return string.Empty;
            }

            var stored = (scene.ShowcaseSubtitleDisplayLookPreset ?? string.Empty).Trim();
            if (stored.Length > 0 && FindByStorage(stored, ShowcaseDisplayLineEffectKind.Body) != null)
            {
                return stored;
            }

            var bold = string.Equals(scene.ShowcaseSubtitleDisplayFontFace, "Bold", StringComparison.OrdinalIgnoreCase);
            var italic = string.Equals(scene.ShowcaseSubtitleDisplayFontFace, "Italic", StringComparison.OrdinalIgnoreCase);
            return MatchLegacyToStorage(
                scene.ShowcaseSubtitleDisplayFontName,
                bold,
                italic,
                scene.ShowcaseSubtitleDisplayPrimaryColourAss,
                scene.ShowcaseSubtitleDisplayDecorPreset,
                ShowcaseDisplayLineEffectKind.Body);
        }

        private static string MatchLegacyToStorage(
            string fontName,
            bool bold,
            bool italic,
            string primaryAss,
            string decorStorage,
            ShowcaseDisplayLineEffectKind kind)
        {
            var font = (fontName ?? string.Empty).Trim();
            var colour = (primaryAss ?? string.Empty).Trim();
            var decor = (decorStorage ?? string.Empty).Trim();

            foreach (var p in AllForKind(kind))
            {
                if (!FontMatches(font, p.FontName, p.Bold))
                {
                    continue;
                }

                if (p.Italic != italic)
                {
                    continue;
                }

                if (colour.Length > 0
                    && !string.Equals(NormalizeAss(colour), NormalizeAss(p.PrimaryAss), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (decor.Length > 0
                    && !string.Equals(decor, p.DecorStorage, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return p.Storage;
            }

            return kind == ShowcaseDisplayLineEffectKind.Hook ? HookDefaultStorage : BodyDefaultStorage;
        }

        private static bool FontMatches(string actual, string presetFont, bool presetBold)
        {
            var a = (actual ?? string.Empty).Trim();
            var p = (presetFont ?? string.Empty).Trim();
            if (a.Length == 0)
            {
                return false;
            }

            if (string.Equals(a, p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (presetBold && a.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0
                           && p.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var baseA = a.Replace(" Bold", string.Empty).Replace("Bold", string.Empty).Trim();
                var baseP = p.Replace(" Bold", string.Empty).Replace("Bold", string.Empty).Trim();
                return string.Equals(baseA, baseP, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string NormalizeAss(string ass)
        {
            return (ass ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
