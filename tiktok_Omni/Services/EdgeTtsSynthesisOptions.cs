namespace tiktok_Omni.Services
{
    public sealed class EdgeTtsSynthesisOptions
    {
        public string VoiceShortName { get; set; } = EdgeTtsVoices.HoaiMyNeural;

        /// <summary>Prosody rate, e.g. +8% for slightly faster / younger delivery.</summary>
        public string Rate { get; set; } = "+0%";

        public string Pitch { get; set; } = "+0Hz";

        public string Volume { get; set; } = "+0%";
    }
}
