namespace tiktok_Omni.Services
{
    public sealed class PhilosophyVideoResult
    {
        public string OutputPath { get; set; } = string.Empty;
        public string Quote { get; set; } = string.Empty;
        public string Mood { get; set; } = string.Empty;
        public string VisualPrompt { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
    }
}
