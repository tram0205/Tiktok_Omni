using System.Drawing;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Theme editor Showcase: jelly hồng sen + jelly xanh (kịch bản/prompt/Loại SP/Chủ đề).</summary>
    internal static class ShowcasePastelTheme
    {
        public static readonly Color ShellBg = Color.FromArgb(20, 22, 30);

        private const float SenTealAccentStrength = 0.60f;
        private const float SenTealFrameStrength = 0.38f;
        private const float JellyAccentStrength = 0.62f;
        private const float JellyFrameStrength = 0.40f;

        // Jelly palette (toolbar Showcase)
        private static readonly Color JellyPink = Color.FromArgb(196, 72, 118);
        private static readonly Color JellyPinkBright = Color.FromArgb(212, 88, 132);
        private static readonly Color JellyTeal = Color.FromArgb(42, 142, 162);
        private static readonly Color JellyTealBright = Color.FromArgb(52, 158, 178);

        private static readonly Color ScriptSen = Color.FromArgb(178, 68, 96);
        private static readonly Color ScriptSenCap = Color.FromArgb(196, 82, 108);
        private static readonly Color PromptTeal = Color.FromArgb(24, 132, 112);
        private static readonly Color PromptTealCap = Color.FromArgb(32, 152, 128);

        // Kịch bản — hồng sen jelly (pha nền)
        public static readonly Color ScriptHeader = BlendOnShell(ScriptSen, SenTealAccentStrength);
        public static readonly Color ScriptFrame = BlendOnShell(ScriptSen, SenTealFrameStrength);
        public static readonly Color ScriptCap = BlendOnShell(ScriptSenCap, SenTealAccentStrength);

        // Prompt clip — xanh jelly / teal (pha nền)
        public static readonly Color PromptHeader = BlendOnShell(PromptTeal, SenTealAccentStrength);
        public static readonly Color PromptFrame = BlendOnShell(PromptTeal, SenTealFrameStrength);
        public static readonly Color PromptCap = BlendOnShell(PromptTealCap, SenTealAccentStrength);

        // Loại SP — hồng jelly
        public static readonly Color ProductHeader = BlendOnShell(JellyPink, JellyAccentStrength);
        public static readonly Color ProductFrame = BlendOnShell(JellyPink, JellyFrameStrength);
        public static readonly Color ProductCap = BlendOnShell(JellyPinkBright, JellyAccentStrength);
        public static readonly Color ProductListSelection = JellyPinkBright;

        // Chủ đề — xanh jelly
        public static readonly Color ThemeHeader = BlendOnShell(JellyTeal, JellyAccentStrength);
        public static readonly Color ThemeFrame = BlendOnShell(JellyTeal, JellyFrameStrength);
        public static readonly Color ThemeCap = BlendOnShell(JellyTealBright, JellyAccentStrength);
        public static readonly Color ThemeListSelection = JellyTealBright;

        public static readonly Color FieldBg = Color.FromArgb(28, 30, 42);
        public static readonly Color FieldBorder = Color.FromArgb(88, 72, 88);
        public static readonly Color ListBg = Color.FromArgb(24, 26, 36);

        public static readonly Color TextPrimary = Color.FromArgb(252, 248, 250);
        public static readonly Color TextBody = Color.FromArgb(236, 232, 238);
        public static readonly Color TextMuted = Color.FromArgb(172, 168, 180);
        public static readonly Color TextSubheader = Color.FromArgb(248, 240, 244);

        public static readonly Color GlassEdge = Color.FromArgb(64, 255, 220, 230);

        public static readonly Color ButtonSave = BlendOnShell(JellyTealBright, 0.72f);
        public static readonly Color ButtonCancel = Color.FromArgb(72, 76, 90);
        public static readonly Color ButtonExcel = BlendOnShell(JellyTealBright, JellyAccentStrength);

        public static readonly Color HintText = Color.FromArgb(152, 148, 162);

        private static Color BlendOnShell(Color accent, float accentWeight)
        {
            if (accentWeight <= 0f)
            {
                return ShellBg;
            }

            if (accentWeight >= 1f)
            {
                return accent;
            }

            var w = accentWeight;
            return Color.FromArgb(
                (int)(ShellBg.R + (accent.R - ShellBg.R) * w),
                (int)(ShellBg.G + (accent.G - ShellBg.G) * w),
                (int)(ShellBg.B + (accent.B - ShellBg.B) * w));
        }
    }
}
