namespace tiktok_Omni.Services
{
    public class HuntProductCandidate
    {
        public string ProfileName { get; set; } = string.Empty;
        public string SourcePlatform { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductLink { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public long SalesVolume { get; set; }
        public decimal Rating { get; set; }
        public string Price { get; set; } = string.Empty;
        public string Commission { get; set; } = string.Empty;
    }
}