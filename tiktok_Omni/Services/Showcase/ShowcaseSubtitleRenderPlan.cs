namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Kế hoạch burn-in phụ đề Showcase — hook và thân bật/tắt độc lập.</summary>
    public sealed class ShowcaseSubtitleRenderPlan
    {
        public bool HookEnabled { get; set; }

        public bool BodyEnabled { get; set; }

        public Services.AssSubtitleGeneratorOptions HookOptions { get; set; }

        public Services.AssSubtitleGeneratorOptions BodyOptions { get; set; }

        public bool HasAnyEnabled => HookEnabled || BodyEnabled;
    }
}
