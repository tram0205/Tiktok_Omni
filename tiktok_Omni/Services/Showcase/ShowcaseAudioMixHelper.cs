using System.Globalization;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Thoại Showcase — FFmpeg amix mặc định normalize=1 chia đều track → lời nói bị nhỏ.</summary>
    internal static class ShowcaseAudioMixHelper
    {
        public const double NarrationPremixGain = 2.0;

        public const string AmixWithMusicSuffix = ":duration=first:dropout_transition=2:normalize=0";

        public const string AmixWithSfxSuffix = ":duration=first:dropout_transition=0.3:normalize=0";

        public static string FormatGain(double linear) =>
            linear.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
