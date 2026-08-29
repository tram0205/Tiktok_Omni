namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Số cảnh tối thiểu Showcase — người dùng tự chọn bao nhiêu ảnh/clip cũng được.</summary>
    internal static class ShowcaseWorkflowConstants
    {
        public const int MinScenes = 1;

        /// <summary>Nhãn nút render mix thành phẩm trong hộp thoại Âm thanh.</summary>
        public const string RenderFullMixedAudioButtonText = "Render Audio";

        public const string RenderFullMixedAudioDialogTitle = "Render Audio";

        public const string ListenFullMixedAudioButtonText = "Nghe thành phẩm";

        public const string ListenFullMixedAudioDialogTitle = "Nghe thành phẩm";

        public static bool HasEnoughScenes(int sceneCount) => sceneCount >= MinScenes;
    }
}
