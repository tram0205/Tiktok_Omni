using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Nền band phía sau cả dòng phụ đề (ASS viền dày / box), không tô từng chữ karaoke.</summary>
    public static class ShowcaseSubtitleHighlightColourCatalog
    {
        public const string FollowLookLabel = "Không nền";

        public sealed class Preset
        {
            public Preset(string label, string storageAss, string rgbAss, byte borderAlphaAss)
            {
                Label = label ?? string.Empty;
                StorageAss = storageAss ?? string.Empty;
                RgbAss = rgbAss ?? string.Empty;
                BorderAlphaAss = borderAlphaAss;
            }

            public string Label { get; }

            /// <summary>&HAABBGGRR (8 hex sau &amp;H) — lưu draft/video.</summary>
            public string StorageAss { get; }

            /// <summary>&H00BBGGRR — màu viền cho tag ASS.</summary>
            public string RgbAss { get; }

            public byte BorderAlphaAss { get; }
        }

        private const string YellowBgr = "00FFFF";
        private const string BlackBgr = "000000";
        private const string WhiteBgr = "FFFFFF";
        private const string PinkBgr = "C080FF";

        private const byte AlphaSolid = 0x00;
        private const byte AlphaM75 = 0x40;
        private const byte AlphaM35 = 0xA6;

        public static readonly Preset[] All =
        {
            new Preset(FollowLookLabel, string.Empty, string.Empty, AlphaSolid),
            new Preset("Vàng · đậm", Storage(YellowBgr, AlphaSolid), Rgb(YellowBgr), AlphaSolid),
            new Preset("Vàng · mờ 75%", Storage(YellowBgr, AlphaM75), Rgb(YellowBgr), AlphaM75),
            new Preset("Vàng · mờ 35%", Storage(YellowBgr, AlphaM35), Rgb(YellowBgr), AlphaM35),
            new Preset("Đen · đậm", Storage(BlackBgr, AlphaSolid), Rgb(BlackBgr), AlphaSolid),
            new Preset("Đen · mờ 75%", Storage(BlackBgr, AlphaM75), Rgb(BlackBgr), AlphaM75),
            new Preset("Đen · mờ 35%", Storage(BlackBgr, AlphaM35), Rgb(BlackBgr), AlphaM35),
            new Preset("Trắng · đậm", Storage(WhiteBgr, AlphaSolid), Rgb(WhiteBgr), AlphaSolid),
            new Preset("Trắng · mờ 75%", Storage(WhiteBgr, AlphaM75), Rgb(WhiteBgr), AlphaM75),
            new Preset("Hồng · đậm", Storage(PinkBgr, AlphaSolid), Rgb(PinkBgr), AlphaSolid),
            new Preset("Hồng · mờ 75%", Storage(PinkBgr, AlphaM75), Rgb(PinkBgr), AlphaM75)
        };

        private static string Rgb(string bbggrr) => "&H00" + bbggrr;

        private static string Storage(string bbggrr, byte assAlpha) =>
            "&H" + assAlpha.ToString("X2", CultureInfo.InvariantCulture) + bbggrr;

        public static void PopulateCombo(ComboBox combo)
        {
            if (combo == null)
            {
                return;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            foreach (var p in All)
            {
                combo.Items.Add(p.Label);
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        public static void PopulateComboColumn(DataGridViewComboBoxColumn column)
        {
            if (column == null)
            {
                return;
            }

            column.Items.Clear();
            foreach (var p in All)
            {
                column.Items.Add(p.Label);
            }
        }

        public static Preset FindByLabel(string label)
        {
            var text = (label ?? string.Empty).Trim();
            return All.FirstOrDefault(p => string.Equals(p.Label, text, StringComparison.Ordinal));
        }

        public static Preset FindByStorage(string storageAss)
        {
            var ass = (storageAss ?? string.Empty).Trim();
            if (ass.Length == 0)
            {
                return All[0];
            }

            var exact = All.FirstOrDefault(p =>
                p.StorageAss.Length > 0
                && string.Equals(p.StorageAss, ass, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            return ResolveLegacyStorage(ass);
        }

        public static string LineBackgroundAssFromLabel(string label)
        {
            var preset = FindByLabel(label);
            return preset?.StorageAss ?? string.Empty;
        }

        public static string SecondaryAssFromLabel(string label) => LineBackgroundAssFromLabel(label);

        public static string LabelFromLineBackgroundAss(string backgroundAss)
        {
            return FindByStorage(backgroundAss).Label;
        }

        public static string LabelFromSecondaryAss(string secondaryAss) => LabelFromLineBackgroundAss(secondaryAss);

        public static void SelectLabel(ComboBox combo, string backgroundAss)
        {
            if (combo == null)
            {
                return;
            }

            var label = LabelFromLineBackgroundAss(backgroundAss);
            if (combo.Items.Contains(label))
            {
                combo.SelectedItem = label;
            }
            else if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        public static void SelectLabelCell(DataGridViewComboBoxCell cell, string backgroundAss)
        {
            if (cell == null)
            {
                return;
            }

            cell.Value = LabelFromLineBackgroundAss(backgroundAss);
        }

        public static Color ToDrawingColor(string storageAss)
        {
            var preset = FindByStorage(storageAss);
            if (preset == null || preset.StorageAss.Length == 0)
            {
                return Color.Empty;
            }

            if (!ShowcaseSubtitleColourPresetCatalog.TryParseAssColour(preset.RgbAss, out var rgb))
            {
                return Color.Empty;
            }

            var visible = 255 - preset.BorderAlphaAss;
            if (visible < 0)
            {
                visible = 0;
            }

            if (visible > 255)
            {
                visible = 255;
            }

            return Color.FromArgb(visible, rgb.R, rgb.G, rgb.B);
        }

        /// <summary>Pha nền band với màu ô lưới — WinForms DGV không blend alpha; dùng cho preview + SelectionBackColor.</summary>
        public static Color ToOpaquePreviewOnBackground(string storageAss, Color gridBackground)
        {
            var band = ToDrawingColor(storageAss);
            if (band == Color.Empty)
            {
                return Color.Empty;
            }

            var a = band.A / 255f;
            var inv = 1f - a;
            var r = (int)Math.Round(gridBackground.R * inv + band.R * a);
            var g = (int)Math.Round(gridBackground.G * inv + band.G * a);
            var b = (int)Math.Round(gridBackground.B * inv + band.B * a);
            return Color.FromArgb(255, ClampByte(r), ClampByte(g), ClampByte(b));
        }

        private static int ClampByte(int v)
        {
            if (v < 0)
            {
                return 0;
            }

            if (v > 255)
            {
                return 255;
            }

            return v;
        }

        public static string BuildLineBackgroundAssTag(string storageAss)
        {
            var preset = FindByStorage(storageAss);
            if (preset == null || preset.StorageAss.Length == 0 || preset.RgbAss.Length == 0)
            {
                return string.Empty;
            }

            var alphaTag = "&H" + preset.BorderAlphaAss.ToString("X2", CultureInfo.InvariantCulture) + "&";
            var rgb = preset.RgbAss;
            return "{\\3c" + rgb + "&\\4c" + rgb + "&\\3a" + alphaTag + "\\4a" + alphaTag
                   + "&\\1bord16\\1shad0\\be0}";
        }

        private static Preset ResolveLegacyStorage(string ass)
        {
            if (!TryParseStorageAss(ass, out var alpha, out var bbggrr, out var legacyEightDigit))
            {
                return All[0];
            }

            var rgbKey = bbggrr.ToUpperInvariant();

            if (string.Equals(rgbKey, YellowBgr, StringComparison.Ordinal)
                || string.Equals(ass, "&H00FFFF00", StringComparison.OrdinalIgnoreCase))
            {
                return PickOpacity(YellowBgr, alpha);
            }

            if (string.Equals(rgbKey, BlackBgr, StringComparison.Ordinal))
            {
                if (legacyEightDigit && alpha == AlphaSolid)
                {
                    return All.First(p => string.Equals(p.Label, "Đen · mờ 75%", StringComparison.Ordinal));
                }

                if (alpha >= AlphaM35)
                {
                    return All.First(p => string.Equals(p.Label, "Đen · mờ 35%", StringComparison.Ordinal));
                }

                if (alpha >= AlphaM75)
                {
                    return All.First(p => string.Equals(p.Label, "Đen · mờ 75%", StringComparison.Ordinal));
                }

                return All.First(p => string.Equals(p.Label, "Đen · đậm", StringComparison.Ordinal));
            }

            if (string.Equals(rgbKey, WhiteBgr, StringComparison.Ordinal))
            {
                return alpha >= AlphaM75
                    ? All.First(p => string.Equals(p.Label, "Trắng · mờ 75%", StringComparison.Ordinal))
                    : All.First(p => string.Equals(p.Label, "Trắng · đậm", StringComparison.Ordinal));
            }

            if (string.Equals(rgbKey, PinkBgr, StringComparison.Ordinal))
            {
                return alpha >= AlphaM75
                    ? All.First(p => string.Equals(p.Label, "Hồng · mờ 75%", StringComparison.Ordinal))
                    : All.First(p => string.Equals(p.Label, "Hồng · đậm", StringComparison.Ordinal));
            }

            if (string.Equals(ass, "&H0000FFFF", StringComparison.OrdinalIgnoreCase))
            {
                return All.First(p => string.Equals(p.Label, "Vàng · đậm", StringComparison.Ordinal));
            }

            return All[0];
        }

        private static Preset PickOpacity(string bbggrr, byte alpha)
        {
            if (alpha >= AlphaM35)
            {
                return All.First(p => string.Equals(p.RgbAss, Rgb(bbggrr), StringComparison.OrdinalIgnoreCase)
                                      && p.BorderAlphaAss == AlphaM35);
            }

            if (alpha >= AlphaM75)
            {
                return All.First(p => string.Equals(p.RgbAss, Rgb(bbggrr), StringComparison.OrdinalIgnoreCase)
                                      && p.BorderAlphaAss == AlphaM75);
            }

            return All.First(p => string.Equals(p.RgbAss, Rgb(bbggrr), StringComparison.OrdinalIgnoreCase)
                                  && p.BorderAlphaAss == AlphaSolid);
        }

        private static bool TryParseStorageAss(string ass, out byte alpha, out string bbggrrSix, out bool legacyEightDigit)
        {
            alpha = AlphaSolid;
            bbggrrSix = string.Empty;
            legacyEightDigit = false;
            var s = (ass ?? string.Empty).Trim();
            if (s.Length < 8 || !s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var hex = s.Substring(2).TrimEnd('&');
            if (hex.Length == 6)
            {
                bbggrrSix = hex.ToUpperInvariant();
                return true;
            }

            if (hex.Length != 8)
            {
                return false;
            }

            legacyEightDigit = hex.StartsWith("00", StringComparison.OrdinalIgnoreCase);
            if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out alpha))
            {
                alpha = AlphaSolid;
            }

            bbggrrSix = hex.Substring(2, 6).ToUpperInvariant();
            return true;
        }
    }
}
