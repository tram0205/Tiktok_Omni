using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>Preset + tùy chỉnh chỉnh màu body video reup (FFmpeg eq filter).</summary>
    public static class ReupColorGradeHelper
    {
        public const string PresetDefault = "default";
        public const string PresetNatural = "natural";
        public const string PresetVivid = "vivid";
        public const string PresetWarm = "warm";
        public const string PresetCool = "cool";
        public const string PresetCinematic = "cinematic";
        public const string PresetCustom = "custom";

        public sealed class ColorGradeValues
        {
            public double Brightness { get; set; }
            public double Contrast { get; set; } = 1d;
            public double Saturation { get; set; } = 1d;
            public double Gamma { get; set; } = 1d;
        }

        private static readonly IReadOnlyList<(string Key, string Label, ColorGradeValues Values)> Presets =
            new List<(string, string, ColorGradeValues)>
            {
                (PresetDefault, "Chuẩn reup", new ColorGradeValues { Brightness = 0.015, Contrast = 1.04, Saturation = 0.95, Gamma = 1.02 }),
                (PresetNatural, "Tự nhiên", new ColorGradeValues { Brightness = 0d, Contrast = 1d, Saturation = 1d, Gamma = 1d }),
                (PresetVivid, "Sống động", new ColorGradeValues { Brightness = 0.03, Contrast = 1.08, Saturation = 1.15, Gamma = 1d }),
                (PresetWarm, "Ấm", new ColorGradeValues { Brightness = 0.02, Contrast = 1.05, Saturation = 1.08, Gamma = 0.98 }),
                (PresetCool, "Mát", new ColorGradeValues { Brightness = 0.01, Contrast = 1.06, Saturation = 0.92, Gamma = 1.03 }),
                (PresetCinematic, "Điện ảnh", new ColorGradeValues { Brightness = -0.02, Contrast = 1.12, Saturation = 0.88, Gamma = 1.08 })
            };

        public static IReadOnlyList<string> PresetDisplayNames =>
            Presets.Select(p => p.Label).ToList();

        public static string GetPresetKeyByDisplayName(string displayName)
        {
            var hit = Presets.FirstOrDefault(p => string.Equals(p.Label, displayName, StringComparison.Ordinal));
            return string.IsNullOrEmpty(hit.Key) ? PresetDefault : hit.Key;
        }

        public static string GetPresetDisplayName(string key)
        {
            var hit = Presets.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrEmpty(hit.Label) ? "Chuẩn reup" : hit.Label;
        }

        public static ColorGradeValues GetPresetValues(string key)
        {
            var hit = Presets.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(hit.Key))
            {
                return Presets[0].Values;
            }

            return CloneValues(hit.Values);
        }

        public static void EnsureRowDefaults(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            var s = settings ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(row.ReupColorPreset))
            {
                row.ReupColorPreset = string.IsNullOrWhiteSpace(s.ReupColorPreset) ? PresetDefault : s.ReupColorPreset.Trim();
            }

            if (string.Equals(row.ReupColorPreset, PresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                if (row.ReupColorContrast <= 0d)
                {
                    row.ReupColorContrast = 1.04d;
                }

                if (row.ReupColorSaturation <= 0d)
                {
                    row.ReupColorSaturation = 0.95d;
                }

                if (row.ReupColorGamma <= 0d)
                {
                    row.ReupColorGamma = 1.02d;
                }
            }

            row.ReupColorGradeLabel = FormatStyleSummary(row);
        }

        public static void ApplyPresetToRow(VideoReupRowItem row, string presetKey)
        {
            if (row == null)
            {
                return;
            }

            row.ReupColorPreset = presetKey ?? PresetDefault;
            if (!string.Equals(row.ReupColorPreset, PresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                var v = GetPresetValues(row.ReupColorPreset);
                row.ReupColorBrightness = v.Brightness;
                row.ReupColorContrast = v.Contrast;
                row.ReupColorSaturation = v.Saturation;
                row.ReupColorGamma = v.Gamma;
            }

            row.ReupColorGradeLabel = FormatStyleSummary(row);
        }

        public static void ResolveValues(VideoReupRowItem row, AppSettings settings, out ColorGradeValues values)
        {
            EnsureRowDefaults(row, settings);
            if (string.Equals(row.ReupColorPreset, PresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                values = new ColorGradeValues
                {
                    Brightness = Clamp(row.ReupColorBrightness, -0.15d, 0.15d),
                    Contrast = Clamp(row.ReupColorContrast <= 0 ? 1d : row.ReupColorContrast, 0.85d, 1.25d),
                    Saturation = Clamp(row.ReupColorSaturation <= 0 ? 1d : row.ReupColorSaturation, 0.75d, 1.35d),
                    Gamma = Clamp(row.ReupColorGamma <= 0 ? 1d : row.ReupColorGamma, 0.88d, 1.15d)
                };
                return;
            }

            values = GetPresetValues(row.ReupColorPreset);
        }

        public static string BuildEqFilterChain(VideoReupRowItem row, AppSettings settings)
        {
            ResolveValues(row, settings, out var v);
            return "eq=brightness=" + v.Brightness.ToString("0.#####", CultureInfo.InvariantCulture) +
                   ":contrast=" + v.Contrast.ToString("0.#####", CultureInfo.InvariantCulture) +
                   ":saturation=" + v.Saturation.ToString("0.#####", CultureInfo.InvariantCulture) +
                   ":gamma=" + v.Gamma.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        public static string FormatStyleSummary(VideoReupRowItem row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var preset = (row.ReupColorPreset ?? PresetDefault).Trim();
            if (string.Equals(preset, PresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                // Đọc trực tiếp từ row — KHÔNG gọi ResolveValues (tránh vòng lặp với EnsureRowDefaults).
                var b = Clamp(row.ReupColorBrightness, -0.15d, 0.15d);
                var c = Clamp(row.ReupColorContrast <= 0d ? 1d : row.ReupColorContrast, 0.85d, 1.25d);
                var s = Clamp(row.ReupColorSaturation <= 0d ? 1d : row.ReupColorSaturation, 0.75d, 1.35d);
                return "Tùy chỉnh · B" + FormatDelta(b) + " C" + FormatPct(c) + " S" + FormatPct(s);
            }

            return GetPresetDisplayName(preset);
        }

        private static string FormatDelta(double value)
        {
            if (Math.Abs(value) < 0.002d)
            {
                return "0";
            }

            return (value >= 0 ? "+" : string.Empty) + (value * 100d).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatPct(double value)
        {
            return (value * 100d).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static ColorGradeValues CloneValues(ColorGradeValues source)
        {
            return new ColorGradeValues
            {
                Brightness = source.Brightness,
                Contrast = source.Contrast,
                Saturation = source.Saturation,
                Gamma = source.Gamma
            };
        }
    }
}
