using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public enum ButtonRole
    {
        Primary,
        Danger,
        MagicAi,
        Neutral
    }

    public static class UIThemeManager
    {
        public static readonly Color PrimaryBack = Color.FromArgb(50, 110, 68);
        public static readonly Color PrimaryOver = Color.FromArgb(65, 135, 85);
        public static readonly Color PrimaryDown = Color.FromArgb(35, 85, 50);

        public static readonly Color DangerBack = Color.FromArgb(160, 50, 50);
        public static readonly Color DangerOver = Color.FromArgb(185, 70, 70);
        public static readonly Color DangerDown = Color.FromArgb(130, 35, 35);

        public static readonly Color MagicAiBack = Color.FromArgb(76, 110, 245);
        public static readonly Color MagicAiOver = Color.FromArgb(100, 130, 255);
        public static readonly Color MagicAiDown = Color.FromArgb(55, 85, 210);

        public static readonly Color NeutralBack = Color.FromArgb(60, 64, 77);
        public static readonly Color NeutralOver = Color.FromArgb(80, 85, 100);
        public static readonly Color NeutralDown = Color.FromArgb(45, 48, 58);

        public static readonly Color ButtonFore = Color.WhiteSmoke;

        private static readonly string[] DangerKeywords =
        {
            "STOP", "CLEAR", "REMOVE", "REJECT", "EMERGENCY", "DELETE",
            "XÓA", "LÀM SẠCH", "DỪNG", "KHẨN", "TỪ CHỐI"
        };

        private static readonly string[] MagicAiKeywords =
        {
            "GENERATE", "LYRIA", "GEMINI", "VOICEOVER", "DEEPDIVE", "DEEP DIVE", "VEO",
            "MAGIC", "GPT", "AIVIDEO", "AIGEN", "PROMPT AI"
        };

        private static readonly string[] MagicAiNameHints =
        {
            "GEMINI", "LYRIA", "VEO", "DEEPDIVE", "DEEPVIDEO", "AIVIDEO", "AIGEN", "GENERATEPROMPT", "MAGICAI"
        };

        private static readonly string[] PrimaryKeywords =
        {
            "START", "RUN", "RENDER", "SAVE", "PUSH", "ADD", "APPROVE", "PUBLISH",
            "THÊM", "LƯU", "DUYỆT", "NHẬP", "CHẠY", "TẠO VIDEO", "ĐĂNG", "EXECUTE", "SUBMIT", "CONFIRM"
        };

        private static readonly string[] NeutralKeywords =
        {
            "BROWSE", "OPEN", "COPY", "REFRESH", "SETTINGS", "CLOSE", "OPTION", "VIEW", "LOG", "FOLDER",
            "MỞ", "SAO CHÉP", "LÀM MỚI", "CÀI ĐẶT", "ĐÓNG", "DUYỆT…", "OUTPUT", "HISTORY", "FILTER", "LOAD"
        };

        private static readonly string[] GridDangerHeaders = { "XÓA", "DELETE", "REMOVE", "REJECT" };
        private static readonly string[] GridPrimaryHeaders = { "RUN", "START", "APPROVE", "SAVE", "RENDER", "PUSH", "ADD", "EXEC" };
        private static readonly string[] GridMagicHeaders = { "GEMINI", "AI", "GENERATE", "LYRIA", "VEO" };
        private static readonly string[] GridNeutralHeaders = { "SỬA", "EDIT", "BROWSE", "OPEN", "COPY", "REFRESH", "VIEW" };

        public static Color GetBackColor(ButtonRole role) => GetRoleColors(role).Back;

        public static Color GetMouseOverBackColor(ButtonRole role) => GetRoleColors(role).Over;

        public static Color GetMouseDownBackColor(ButtonRole role) => GetRoleColors(role).Down;

        private static (Color Back, Color Over, Color Down) GetRoleColors(ButtonRole role)
        {
            switch (role)
            {
                case ButtonRole.Primary:
                    return (PrimaryBack, PrimaryOver, PrimaryDown);
                case ButtonRole.Danger:
                    return (DangerBack, DangerOver, DangerDown);
                case ButtonRole.MagicAi:
                    return (MagicAiBack, MagicAiOver, MagicAiDown);
                default:
                    return (NeutralBack, NeutralOver, NeutralDown);
            }
        }

        public static ButtonRole InferRole(string name, string text)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(text))
            {
                return ButtonRole.Neutral;
            }

            var haystack = ((name ?? string.Empty) + " " + (text ?? string.Empty)).ToUpperInvariant();

            if (ContainsKeyword(haystack, DangerKeywords))
            {
                return ButtonRole.Danger;
            }

            if (ContainsKeyword(haystack, MagicAiKeywords) || ContainsMagicAiNameHint(name))
            {
                return ButtonRole.MagicAi;
            }

            if (ContainsKeyword(haystack, PrimaryKeywords))
            {
                return ButtonRole.Primary;
            }

            if (ContainsKeyword(haystack, NeutralKeywords))
            {
                return ButtonRole.Neutral;
            }

            return ButtonRole.Neutral;
        }

        public static ButtonRole InferGridColumnRole(string headerText)
        {
            var haystack = (headerText ?? string.Empty).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(haystack))
            {
                return ButtonRole.Neutral;
            }

            if (ContainsKeyword(haystack, GridDangerHeaders))
            {
                return ButtonRole.Danger;
            }

            if (ContainsKeyword(haystack, GridMagicHeaders))
            {
                return ButtonRole.MagicAi;
            }

            if (ContainsKeyword(haystack, GridPrimaryHeaders))
            {
                return ButtonRole.Primary;
            }

            if (ContainsKeyword(haystack, GridNeutralHeaders))
            {
                return ButtonRole.Neutral;
            }

            return ButtonRole.Neutral;
        }

        public static void ApplyTheme(this Button btn, ButtonRole role)
        {
            if (btn == null)
            {
                return;
            }

            var colors = GetRoleColors(role);

            btn.FlatStyle = FlatStyle.Flat;
            btn.UseVisualStyleBackColor = false;
            btn.ForeColor = ButtonFore;
            btn.BackColor = colors.Back;
            btn.Tag = role;

            var flat = btn.FlatAppearance;
            flat.BorderSize = 0;
            flat.MouseOverBackColor = colors.Over;
            flat.MouseDownBackColor = colors.Down;
        }

        public static void ApplyGridColumnTheme(DataGridViewColumn column, ButtonRole role)
        {
            if (column == null)
            {
                return;
            }

            var back = GetBackColor(role);
            column.DefaultCellStyle.BackColor = back;
            column.DefaultCellStyle.ForeColor = ButtonFore;
            column.DefaultCellStyle.SelectionBackColor = Color.FromArgb(
                Math.Min(255, back.R + 30),
                Math.Min(255, back.G + 30),
                Math.Min(255, back.B + 30));
            column.DefaultCellStyle.SelectionForeColor = ButtonFore;
        }

        private static bool ContainsKeyword(string haystack, string[] keywords)
        {
            return keywords.Any(k => haystack.IndexOf(k, StringComparison.Ordinal) >= 0);
        }

        private static bool ContainsMagicAiNameHint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var upperName = name.ToUpperInvariant();
            return MagicAiNameHints.Any(h =>
                upperName.IndexOf(h, StringComparison.Ordinal) >= 0);
        }
    }
}
