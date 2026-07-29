using System.Collections.Generic;

namespace tiktok_Omni.Services.Showcase
{
    internal enum ShowcaseOverviewStepStatus
    {
        Complete,
        Partial,
        Missing
    }

    internal sealed class ShowcaseRenderOverviewSnapshot
    {
        public string ProductTitle { get; set; } = string.Empty;

        public string Theme { get; set; } = string.Empty;

        public string AspectLabel { get; set; } = string.Empty;

        public bool RenderReady { get; set; }

        public string HeroSummary { get; set; } = string.Empty;

        public string ClipsDir { get; set; } = string.Empty;

        public string ScriptPreviewText { get; set; } = string.Empty;

        public IList<ShowcaseOverviewPipelineStep> Pipeline { get; set; } = new List<ShowcaseOverviewPipelineStep>();

        public IList<ShowcaseOverviewSceneCard> SceneCards { get; set; } = new List<ShowcaseOverviewSceneCard>();

        public ShowcaseOverviewAudioSubtitlePanel AudioSubtitle { get; set; } = new ShowcaseOverviewAudioSubtitlePanel();

        public IList<ShowcaseRenderOverviewLine> Lines { get; set; } = new List<ShowcaseRenderOverviewLine>();

        public IList<string> RenderBlockers { get; set; } = new List<string>();
    }

    internal sealed class ShowcaseOverviewPipelineStep
    {
        public string Title { get; set; } = string.Empty;

        public ShowcaseOverviewStepStatus Status { get; set; }

        public string Detail { get; set; } = string.Empty;
    }

    internal sealed class ShowcaseOverviewSceneCard
    {
        public int SceneIndex { get; set; }

        public string ThumbnailPath { get; set; } = string.Empty;

        public bool HasClip { get; set; }

        public bool HasPrompt { get; set; }

        public bool IsSilent { get; set; }

        public string VoicePreview { get; set; } = string.Empty;

        /// <summary>Thoại đầy đủ (không rút gọn) cho lưới tổng quan.</summary>
        public string VoiceText { get; set; } = string.Empty;

        /// <summary>Chữ burn-in hiển thị trên video (sau khi rút gọn / chỉnh phụ đề).</summary>
        public string SubtitleDisplayText { get; set; } = string.Empty;

        public string ClipToolLabel { get; set; } = string.Empty;
    }

    internal sealed class ShowcaseOverviewAudioSubtitlePanel
    {
        public string HookText { get; set; } = string.Empty;

        public string CtaText { get; set; } = string.Empty;

        public bool VoiceAligned { get; set; }

        public string VoiceSummary { get; set; } = string.Empty;

        public bool SubtitleBurnIn { get; set; }

        public string SubtitleLook { get; set; } = string.Empty;

        public string SubtitleHookOnScreen { get; set; } = string.Empty;

        public string SubtitleCtaOnScreen { get; set; } = string.Empty;

        public string MusicLabel { get; set; } = string.Empty;

        public string MusicVolume { get; set; } = string.Empty;

        public bool HasNarration { get; set; }

        public string NarrationLabel { get; set; } = string.Empty;

        public string SpeedLabel { get; set; } = string.Empty;

        public string SfxLabel { get; set; } = string.Empty;

        public string TransitionLabel { get; set; } = string.Empty;
    }

    internal sealed class ShowcaseRenderOverviewLine
    {
        public string Section { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public bool Missing { get; set; }
    }
}
